
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
        return await _context.FinancialTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.TransactionDateUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<FinancialTransaction?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.FinancialTransactions
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                ct);
    }

    public async Task<FinancialAccount?> GetAccountAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.FinancialAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == accountId && x.UserId == userId,
                ct);
    }

    public async Task<TransactionCategory?> GetCategoryAsync(
        Guid categoryId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.TransactionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == categoryId && x.UserId == userId,
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
}
