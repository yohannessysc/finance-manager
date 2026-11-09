using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class CsvTransactionImportService : ICsvTransactionImportService
{
    private const int MaximumRecords = 5_000;
    private const int MaximumReportedErrors = 100;
    private const decimal MaximumAmount = 9999999999999999.99m;
    private readonly FinanceDbContext _db;
    private readonly IFinancialTransactionRepository _transactions;

    public CsvTransactionImportService(
        FinanceDbContext db,
        IFinancialTransactionRepository transactions)
    {
        _db = db;
        _transactions = transactions;
    }

    public async Task<CsvTransactionImportResponse> ImportAsync(
        Guid userId,
        Stream csvStream,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(
            csvStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var content = await reader.ReadToEndAsync(ct);
        var records = CsvRecordParser.Parse(content);

        if (records.Count < 2)
            throw new ArgumentException("The CSV must include a header and at least one data row.");
        if (records.Count - 1 > MaximumRecords)
            throw new ArgumentException($"A CSV can contain at most {MaximumRecords} data rows.");

        var columns = GetColumns(records[0]);
        var timeZoneId = await _db.UserPreferences
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TimeZone)
            .FirstOrDefaultAsync(ct) ?? "America/Toronto";
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var householdIds = await _db.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);
        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId ||
                (x.HouseholdId.HasValue &&
                 householdIds.Contains(x.HouseholdId.Value)))
            .ToListAsync(ct);
        var categories = await _db.TransactionCategories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var parsedRows = new List<ParsedRow>();
        var errors = new List<CsvTransactionImportError>();
        var invalidRows = 0;

        for (var index = 1; index < records.Count; index++)
        {
            var rowNumber = index + 1;
            var row = records[index];

            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            try
            {
                var parsed = ParseRow(
                    row,
                    columns,
                    accounts,
                    categories,
                    timeZone);
                if (!await _transactions.CanEditAccountAsync(
                        parsed.Account.Id,
                        userId,
                        ct))
                {
                    throw new UnauthorizedAccessException(
                        "Your household role does not allow transaction imports.");
                }

                parsedRows.Add(parsed);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (FormatException exception)
            {
                AddError(rowNumber, exception.Message);
            }
            catch (ArgumentException exception)
            {
                AddError(rowNumber, exception.Message);
            }
        }

        void AddError(int rowNumber, string message)
        {
            invalidRows++;
            if (errors.Count < MaximumReportedErrors)
                errors.Add(new CsvTransactionImportError(rowNumber, message));
        }

        var imported = 0;
        var duplicates = 0;
        if (parsedRows.Count == 0)
        {
            return new CsvTransactionImportResponse(
                imported,
                duplicates,
                invalidRows,
                errors);
        }

        var accountIds = parsedRows
            .Select(x => x.Account.Id)
            .Distinct()
            .ToArray();
        var earliest = parsedRows.Min(x => x.DateUtc);
        var latest = parsedRows.Max(x => x.DateUtc);
        var existingTransactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                accountIds.Contains(x.FinancialAccountId) &&
                x.TransactionDateUtc >= earliest &&
                x.TransactionDateUtc <= latest &&
                (x.Type == TransactionType.Income ||
                 x.Type == TransactionType.Expense))
            .ToListAsync(ct);

        var fingerprints = existingTransactions
            .Select(CreateFingerprint)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var parsed in parsedRows)
        {
            ct.ThrowIfCancellationRequested();
            var fingerprint = CreateFingerprint(
                parsed.Account.Id,
                parsed.DateUtc,
                parsed.Type,
                parsed.Amount,
                parsed.Description,
                parsed.Merchant);

            if (!fingerprints.Add(fingerprint))
            {
                duplicates++;
                continue;
            }

            var transaction = new FinancialTransaction
            {
                UserId = userId,
                FinancialAccountId = parsed.Account.Id,
                CategoryId = parsed.Category?.Id,
                Type = parsed.Type,
                Amount = parsed.Amount,
                Description = parsed.Description,
                Merchant = parsed.Merchant,
                TransactionDateUtc = parsed.DateUtc,
                CreatedAtUtc = DateTime.UtcNow,
                ImportFingerprint = fingerprint
            };

            _db.FinancialTransactions.Add(transaction);
            try
            {
                await _db.SaveChangesAsync(ct);
                imported++;
            }
            catch (DbUpdateException)
            {
                _db.Entry(transaction).State = EntityState.Detached;
                var concurrentDuplicate = await _db.FinancialTransactions
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.UserId == userId &&
                             x.ImportFingerprint == fingerprint,
                        ct);

                if (!concurrentDuplicate)
                    throw;

                duplicates++;
            }
        }

        return new CsvTransactionImportResponse(
            imported,
            duplicates,
            invalidRows,
            errors);
    }

    private static ParsedRow ParseRow(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<FinancialAccount> accounts,
        IReadOnlyList<TransactionCategory> categories,
        TimeZoneInfo timeZone)
    {
        string Value(string key)
        {
            if (!columns.TryGetValue(key, out var column) ||
                column >= values.Count)
            {
                return string.Empty;
            }

            return values[column].Trim();
        }

        var accountName = Value("account");
        if (accountName.Length == 0)
            throw new FormatException("Account is required.");

        var currency = Value("currency");
        var matchingAccounts = accounts
            .Where(x =>
                string.Equals(
                    x.Name,
                    accountName,
                    StringComparison.OrdinalIgnoreCase) &&
                (currency.Length == 0 ||
                 string.Equals(
                     x.Currency,
                     currency,
                     StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matchingAccounts.Count == 0)
            throw new FormatException($"Account '{accountName}' was not found.");
        if (matchingAccounts.Count > 1)
            throw new FormatException(
                $"Account '{accountName}' is ambiguous; include a currency column.");

        var description = Value("description");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500)
            throw new FormatException("Description is required and cannot exceed 500 characters.");

        if (!decimal.TryParse(
                Value("amount"),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsedAmount) ||
            parsedAmount == 0 ||
            Math.Abs(parsedAmount) > MaximumAmount)
        {
            throw new FormatException(
                "Amount must be a non-zero number using a decimal point and no more than two decimal places.");
        }

        if (decimal.Round(Math.Abs(parsedAmount), 2) != Math.Abs(parsedAmount))
            throw new FormatException("Amount cannot have more than two decimal places.");

        var typeValue = Value("type");
        TransactionType type;
        decimal amount;
        if (typeValue.Length == 0)
        {
            type = parsedAmount > 0
                ? TransactionType.Income
                : TransactionType.Expense;
            amount = Math.Abs(parsedAmount);
        }
        else
        {
            type = typeValue.Equals("income", StringComparison.OrdinalIgnoreCase)
                ? TransactionType.Income
                : typeValue.Equals("expense", StringComparison.OrdinalIgnoreCase)
                    ? TransactionType.Expense
                    : throw new FormatException(
                        "Type must be Income or Expense; transfers are not supported by CSV import.");

            if (parsedAmount < 0)
                throw new FormatException(
                    "When Type is provided, Amount must be positive.");
            amount = parsedAmount;
        }

        var dateUtc = ParseDate(Value("date"), timeZone);
        var merchant = Value("merchant");
        if (merchant.Length > 200)
            throw new FormatException("Merchant cannot exceed 200 characters.");

        TransactionCategory? category = null;
        var categoryName = Value("category");
        if (categoryName.Length > 0)
        {
            var matches = categories.Where(x =>
                x.Type == type &&
                string.Equals(
                    x.Name,
                    categoryName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                throw new FormatException(
                    $"Category '{categoryName}' was not found for {type}.");
            if (matches.Count > 1)
                throw new FormatException(
                    $"Category '{categoryName}' is ambiguous for {type}.");
            category = matches[0];
        }

        return new ParsedRow(
            matchingAccounts[0],
            category,
            type,
            amount,
            description,
            merchant.Length == 0 ? null : merchant,
            dateUtc);
    }

    private static DateTime ParseDate(
        string value,
        TimeZoneInfo timeZone)
    {
        if (DateOnly.TryParseExact(
                value,
                ["yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            var localMidnight = DateTime.SpecifyKind(
                dateOnly.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);
            return NormalizePrecision(
                TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone));
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var dateWithOffset) &&
            HasExplicitOffset(value))
        {
            return NormalizePrecision(dateWithOffset.UtcDateTime);
        }

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var localDate))
        {
            throw new FormatException(
                "Date must be an ISO date, a supported date, or an ISO timestamp.");
        }

        return NormalizePrecision(TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified),
            timeZone));
    }

    private static bool HasExplicitOffset(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
               (trimmed.Length >= 6 &&
                (trimmed[^6] == '+' || trimmed[^6] == '-') &&
                char.IsDigit(trimmed[^5]) &&
                char.IsDigit(trimmed[^4]) &&
                trimmed[^3] == ':' &&
                char.IsDigit(trimmed[^2]) &&
                char.IsDigit(trimmed[^1])) ||
               (trimmed.Length >= 5 &&
                (trimmed[^5] == '+' || trimmed[^5] == '-') &&
                char.IsDigit(trimmed[^4]) &&
                char.IsDigit(trimmed[^3]) &&
                char.IsDigit(trimmed[^2]) &&
                char.IsDigit(trimmed[^1]));
    }

    private static DateTime NormalizePrecision(DateTime value) =>
        new(value.Ticks / 10 * 10, DateTimeKind.Utc);

    private static Dictionary<string, int> GetColumns(
        IReadOnlyList<string> header)
    {
        var columns = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < header.Count; index++)
        {
            var name = header[index].Trim().ToLowerInvariant();
            if (!columns.TryAdd(name, index))
                throw new ArgumentException($"Duplicate CSV column '{name}'.");
        }

        foreach (var required in new[]
                 {
                     "date", "description", "amount", "account"
                 })
        {
            if (!columns.ContainsKey(required))
                throw new ArgumentException(
                    $"CSV is missing required column '{required}'.");
        }

        var supported = new HashSet<string>(
            ["date", "description", "amount", "account", "currency",
             "type", "category", "merchant"],
            StringComparer.OrdinalIgnoreCase);
        var unknown = columns.Keys.FirstOrDefault(x => !supported.Contains(x));
        if (unknown is not null)
            throw new ArgumentException($"CSV contains unsupported column '{unknown}'.");

        return columns;
    }

    private static string CreateFingerprint(FinancialTransaction transaction) =>
        CreateFingerprint(
            transaction.FinancialAccountId,
            transaction.TransactionDateUtc,
            transaction.Type,
            transaction.Amount,
            transaction.Description,
            transaction.Merchant);

    private static string CreateFingerprint(
        Guid accountId,
        DateTime dateUtc,
        TransactionType type,
        decimal amount,
        string description,
        string? merchant)
    {
        var canonical = string.Join(
            '\n',
            accountId.ToString("N"),
            (dateUtc.Ticks / 10 * 10)
                .ToString(CultureInfo.InvariantCulture),
            ((int)type).ToString(CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            NormalizeText(description),
            NormalizeText(merchant ?? string.Empty));

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string NormalizeText(string value) =>
        string.Join(
            ' ',
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private sealed record ParsedRow(
        FinancialAccount Account,
        TransactionCategory? Category,
        TransactionType Type,
        decimal Amount,
        string Description,
        string? Merchant,
        DateTime DateUtc);
}

internal static class CsvRecordParser
{
    public static List<List<string>> Parse(string content)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var quoteClosed = false;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (inQuotes)
            {
                if (current != '"')
                {
                    field.Append(current);
                }
                else if (index + 1 < content.Length &&
                         content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = false;
                    quoteClosed = true;
                }

                continue;
            }

            if (quoteClosed &&
                current != ',' &&
                current != '\r' &&
                current != '\n')
            {
                throw new FormatException(
                    "Unexpected character after a quoted CSV field.");
            }

            if (current == '"')
            {
                if (field.Length != 0 || quoteClosed)
                    throw new FormatException("Invalid quote in CSV field.");
                inQuotes = true;
            }
            else if (current == ',')
            {
                row.Add(field.ToString().Trim());
                field.Clear();
                quoteClosed = false;
            }
            else if (current is '\r' or '\n')
            {
                if (current == '\r' &&
                    index + 1 < content.Length &&
                    content[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString().Trim());
                field.Clear();
                quoteClosed = false;
                if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
                    records.Add(row);
                row = [];
            }
            else
            {
                field.Append(current);
            }
        }

        if (inQuotes)
            throw new FormatException("CSV contains an unterminated quoted field.");

        if (field.Length > 0 || row.Count > 0 || quoteClosed)
        {
            row.Add(field.ToString().Trim());
            if (row.Any(x => !string.IsNullOrWhiteSpace(x)))
                records.Add(row);
        }

        return records;
    }
}
