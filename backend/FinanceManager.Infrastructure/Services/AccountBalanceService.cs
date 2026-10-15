
using FinanceManager.Application.DTOs.Accounts;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public class AccountBalanceService : IAccountBalanceService
{
    private readonly FinanceDbContext _context;

    public AccountBalanceService(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<AccountBalanceResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var accounts = await _context.FinancialAccounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var transactions = await _context.FinancialTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        return accounts.Select(account =>
            CalculateBalance(account.Id,
                account.Name,
                account.Currency,
                account.OpeningBalance,
                transactions)).ToList();
    }

    public async Task<AccountBalanceResponse?> GetByAccountIdAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default)
    {
        var account = await _context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == accountId && x.UserId == userId,
                ct);

        if (account is null)
            return null;

        var transactions = await _context.FinancialTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                (x.FinancialAccountId == accountId ||
                 x.DestinationAccountId == accountId))
            .ToListAsync(ct);

        return CalculateBalance(
            account.Id,
            account.Name,
            account.Currency,
            account.OpeningBalance,
            transactions);
    }

    private static AccountBalanceResponse CalculateBalance(
        Guid accountId,
        string accountName,
        string currency,
        decimal openingBalance,
        List<FinanceManager.Domain.Entities.FinancialTransaction> transactions)
    {
        var income = transactions
            .Where(x => x.FinancialAccountId == accountId &&
                        x.Type == TransactionType.Income)
            .Sum(x => x.Amount);

        var expenses = transactions
            .Where(x => x.FinancialAccountId == accountId &&
                        x.Type == TransactionType.Expense)
            .Sum(x => x.Amount);

        var incomingTransfers = transactions
            .Where(x => x.DestinationAccountId == accountId &&
                        x.Type == TransactionType.Transfer)
            .Sum(x => x.Amount);

        var outgoingTransfers = transactions
            .Where(x => x.FinancialAccountId == accountId &&
                        x.Type == TransactionType.Transfer)
            .Sum(x => x.Amount);

        var currentBalance =
            openingBalance +
            income -
            expenses +
            incomingTransfers -
            outgoingTransfers;

        return new AccountBalanceResponse(
            accountId,
            accountName,
            currency,
            openingBalance,
            income,
            expenses,
            incomingTransfers,
            outgoingTransfers,
            currentBalance);
    }
}
