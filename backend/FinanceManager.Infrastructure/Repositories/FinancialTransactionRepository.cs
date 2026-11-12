
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class FinancialTransactionRepository
    : IFinancialTransactionRepository
{
    private readonly FinanceDbContext _context;

    public FinancialTransactionRepository(
        FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<FinancialTransaction>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);
        var accountIds = _context.FinancialAccounts
            .Where(x =>
                x.UserId == userId ||
                (x.HouseholdId.HasValue &&
                 householdIds.Contains(x.HouseholdId.Value)))
            .Select(x => x.Id);

        return await _context.FinancialTransactions
            .AsNoTracking()
            .Where(x =>
                accountIds.Contains(x.FinancialAccountId) ||
                (x.DestinationAccountId.HasValue &&
                 accountIds.Contains(x.DestinationAccountId.Value)))
            .OrderByDescending(x => x.TransactionDateUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<FinancialTransaction?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);
        var accountIds = _context.FinancialAccounts
            .Where(x =>
                x.UserId == userId ||
                (x.HouseholdId.HasValue &&
                 householdIds.Contains(x.HouseholdId.Value)))
            .Select(x => x.Id);

        return await _context.FinancialTransactions
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     (accountIds.Contains(x.FinancialAccountId) ||
                      (x.DestinationAccountId.HasValue &&
                       accountIds.Contains(x.DestinationAccountId.Value))),
                ct);
    }

    public async Task<FinancialAccount?> GetAccountAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);

        return await _context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == accountId &&
                     (x.UserId == userId ||
                      (x.HouseholdId.HasValue &&
                       householdIds.Contains(x.HouseholdId.Value))),
                ct);
    }

    public async Task<TransactionCategory?> GetCategoryAsync(
        Guid categoryId,
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);
        var householdMemberIds = _context.HouseholdMembers
            .Where(x => householdIds.Contains(x.HouseholdId))
            .Select(x => x.UserId);

        return await _context.TransactionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == categoryId &&
                     (x.UserId == userId ||
                      householdMemberIds.Contains(x.UserId)),
                ct);
    }

    public async Task<bool> CanEditAccountAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default)
    {
        var account = await _context.FinancialAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == accountId, ct);
        if (account is null)
            return false;
        if (account.UserId == userId)
            return true;
        if (account.HouseholdId is null)
            return false;

        return await _context.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.HouseholdId == account.HouseholdId &&
                     x.UserId == userId &&
                     (x.Role == FinanceManager.Domain.Enums.HouseholdRole.Owner ||
                      x.Role == FinanceManager.Domain.Enums.HouseholdRole.Admin ||
                      x.Role == FinanceManager.Domain.Enums.HouseholdRole.Editor),
                ct);
    }

    public async Task<bool> CanEditTransactionAsync(
        Guid transactionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var account = await (
            from transaction in _context.FinancialTransactions.AsNoTracking()
            join financialAccount in _context.FinancialAccounts.AsNoTracking()
                on transaction.FinancialAccountId equals financialAccount.Id
            where transaction.Id == transactionId
            select financialAccount)
            .SingleOrDefaultAsync(ct);

        if (account is null)
            return false;
        if (account.UserId == userId)
            return true;
        if (account.HouseholdId is null)
            return false;

        return await _context.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.HouseholdId == account.HouseholdId &&
                     x.UserId == userId &&
                     (x.Role == FinanceManager.Domain.Enums.HouseholdRole.Owner ||
                      x.Role == FinanceManager.Domain.Enums.HouseholdRole.Admin ||
                      x.Role == FinanceManager.Domain.Enums.HouseholdRole.Editor),
                ct);
    }

    public async Task AddAsync(
        FinancialTransaction transaction,
        CancellationToken ct = default)
    {
        _context.FinancialTransactions.Add(transaction);

        await _context.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(
        CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        FinancialTransaction transaction,
        CancellationToken ct = default)
    {
        _context.FinancialTransactions.Remove(transaction);

        await _context.SaveChangesAsync(ct);
    }

    public Task<List<TransactionSplit>> GetSplitsAsync(
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken ct = default) =>
        _context.TransactionSplits
            .AsNoTracking()
            .Where(x => transactionIds.Contains(x.TransactionId))
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);

    public async Task ReplaceSplitsAsync(
        Guid transactionId,
        IEnumerable<TransactionSplit> splits,
        CancellationToken ct = default)
    {
        var existing = await _context.TransactionSplits
            .Where(x => x.TransactionId == transactionId)
            .ToListAsync(ct);

        _context.TransactionSplits.RemoveRange(existing);
        _context.TransactionSplits.AddRange(splits);
    }

    public Task<Dictionary<Guid, int>> GetAttachmentCountsAsync(
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken ct = default) =>
        _context.TransactionAttachments
            .AsNoTracking()
            .Where(x => transactionIds.Contains(x.TransactionId))
            .GroupBy(x => x.TransactionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}
