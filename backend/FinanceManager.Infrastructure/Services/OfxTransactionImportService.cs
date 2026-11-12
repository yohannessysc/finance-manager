using System.Security.Cryptography;
using System.Text;

using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class OfxTransactionImportService : IOfxTransactionImportService
{
    private const int MaximumTransactions = 5_000;
    private const decimal MaximumAmount = 9999999999999999.99m;
    private const string DefaultTimeZoneId = "America/Toronto";

    private readonly FinanceDbContext _db;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly ICategoryRuleService _rules;

    public OfxTransactionImportService(
        FinanceDbContext db,
        IFinancialTransactionRepository transactions,
        ICategoryRuleService rules)
    {
        _db = db;
        _transactions = transactions;
        _rules = rules;
    }

    public async Task<CsvTransactionImportResponse> ImportAsync(
        Guid userId,
        Guid accountId,
        Stream stream,
        CancellationToken ct = default)
    {
        var account = await _transactions.GetAccountAsync(accountId, userId, ct)
            ?? throw new ArgumentException("Choose one of your accounts to import into.");

        if (!await _transactions.CanEditAccountAsync(accountId, userId, ct))
            throw new UnauthorizedAccessException("Your household role does not allow transaction imports.");

        if (account.BankConnectionId is not null)
        {
            throw new ArgumentException(
                "This account already syncs from your bank, so importing a statement would duplicate its transactions.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var statement = OfxParser.Parse(await reader.ReadToEndAsync(ct));

        if (statement.Transactions.Count == 0)
            throw new ArgumentException("The statement has no transactions.");
        if (statement.Transactions.Count > MaximumTransactions)
            throw new ArgumentException($"A statement can contain at most {MaximumTransactions} transactions.");
        if (statement.Currency is { } currency &&
            !string.Equals(currency, account.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The statement is in {currency} but {account.Name} is in {account.Currency}.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            await _db.UserPreferences
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.TimeZone)
                .FirstOrDefaultAsync(ct) ?? DefaultTimeZoneId);

        var categoryTypes = await _db.TransactionCategories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Id, x => x.Type, ct);

        var fingerprints = statement.Transactions
            .Select(x => Fingerprint(accountId, x.FitId))
            .ToList();
        var existing = (await _db.FinancialTransactions
                .AsNoTracking()
                .Where(x => x.FinancialAccountId == accountId &&
                            x.ImportFingerprint != null &&
                            fingerprints.Contains(x.ImportFingerprint))
                .Select(x => x.ImportFingerprint!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var imported = 0;
        var duplicates = 0;
        var invalid = 0;
        var errors = new List<CsvTransactionImportError>();
        var row = 0;

        foreach (var item in statement.Transactions)
        {
            ct.ThrowIfCancellationRequested();
            row++;

            var fingerprint = Fingerprint(accountId, item.FitId);
            if (!existing.Add(fingerprint))
            {
                duplicates++;
                continue;
            }

            var amount = Math.Abs(item.Amount);
            if (amount == 0 || amount > MaximumAmount || decimal.Round(amount, 2) != amount)
            {
                invalid++;
                if (errors.Count < 100)
                    errors.Add(new CsvTransactionImportError(row, $"Skipped \"{item.Name}\": unsupported amount {item.Amount}."));
                continue;
            }

            var type = item.Amount < 0 ? TransactionType.Expense : TransactionType.Income;
            var description = item.Memo is { } memo && !memo.Equals(item.Name, StringComparison.OrdinalIgnoreCase)
                ? $"{item.Name} — {memo}"
                : item.Name;

            // The user's categorization rules apply as they do to bank sync.
            var rule = await _rules.EvaluateTransactionAsync(userId, description, item.Name, amount, (int)type);
            var categoryId = rule.CategoryId is { } ruleCategory &&
                             categoryTypes.GetValueOrDefault(ruleCategory) == type
                ? rule.CategoryId
                : null;

            _db.FinancialTransactions.Add(new FinancialTransaction
            {
                UserId = userId,
                FinancialAccountId = accountId,
                CategoryId = categoryId,
                Type = type,
                Amount = amount,
                Description = Truncate(description, 500),
                Merchant = Truncate(item.Name, 200),
                // Statements post by calendar day: start of that day locally.
                TransactionDateUtc = TimeZoneInfo.ConvertTimeToUtc(
                    item.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                    timeZone),
                CreatedAtUtc = DateTime.UtcNow,
                ImportFingerprint = fingerprint
            });
            imported++;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another import of the same statement saved first.
            throw new ArgumentException(
                "Some of these transactions were imported at the same time. Try the import again.");
        }

        return new CsvTransactionImportResponse(imported, duplicates, invalid, errors);
    }

    // The bank's transaction id (FITID) is unique per account, so it
    // identifies a transaction across overlapping statement downloads.
    private static string Fingerprint(Guid accountId, string fitId) =>
        "ofx:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{accountId:N}\n{fitId}")))[..56];

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
