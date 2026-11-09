
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class RecurringTransactionRepository
    : IRecurringTransactionRepository
{
    private readonly FinanceDbContext _context;

    public RecurringTransactionRepository(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<RecurringTransaction>> GetAllAsync(
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

        return await _context.RecurringTransactions
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.FinancialAccountId))
            .OrderBy(x => x.NextOccurrenceUtc)
            .ToListAsync(ct);
    }

    public async Task<RecurringTransaction?> GetByIdAsync(
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

        return await _context.RecurringTransactions
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     accountIds.Contains(x.FinancialAccountId),
                ct);
    }

    public async Task AddAsync(
        RecurringTransaction transaction,
        CancellationToken ct = default)
    {
        _context.RecurringTransactions.Add(transaction);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(
        CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        RecurringTransaction transaction,
        CancellationToken ct = default)
    {
        _context.RecurringTransactions.Remove(transaction);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<DateTime?> GetLastPostedOccurrenceAsync(
    Guid scheduleId,
    Guid userId,
    CancellationToken ct = default)
    {
        var householdIds = await _context.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);
        var scheduleExists = await _context.RecurringTransactions
            .AnyAsync(
                x => x.Id == scheduleId &&
                     _context.FinancialAccounts.Any(account =>
                         account.Id == x.FinancialAccountId &&
                         (account.UserId == userId ||
                          (account.HouseholdId.HasValue &&
                           householdIds.Contains(account.HouseholdId.Value)))),
                ct);
        if (!scheduleExists)
            return null;

        return await _context.FinancialTransactions
            .AsNoTracking()
            .Where(x =>
                x.RecurringTransactionId == scheduleId &&
                x.ScheduledOccurrenceUtc.HasValue)
            .MaxAsync(
                x => x.ScheduledOccurrenceUtc,
                ct);
    }
}
