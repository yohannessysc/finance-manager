
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class TransactionCategoryRepository
    : ITransactionCategoryRepository
{
    private readonly FinanceDbContext _context;

    public TransactionCategoryRepository(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<TransactionCategory>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.TransactionCategories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<TransactionCategory?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.TransactionCategories
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                ct);
    }

    public async Task<bool> NameExistsAsync(
        Guid userId,
        TransactionType type,
        string name,
        Guid? excludeId = null,
        CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();

        return await _context.TransactionCategories
            .AnyAsync(
                x => x.UserId == userId &&
                     x.Type == type &&
                     x.Name.ToLower() == normalized &&
                     (!excludeId.HasValue || x.Id != excludeId.Value),
                ct);
    }

    public async Task<bool> IsInUseAsync(
        Guid categoryId,
        Guid userId,
        CancellationToken ct = default)
    {
        var usedInTransactions =
            await _context.FinancialTransactions.AnyAsync(
                x => x.UserId == userId &&
                     x.CategoryId == categoryId,
                ct);

        if (usedInTransactions)
            return true;

        return await _context.RecurringTransactions.AnyAsync(
            x => x.UserId == userId &&
                 x.CategoryId == categoryId,
            ct);
    }

    public async Task AddAsync(
        TransactionCategory category,
        CancellationToken ct = default)
    {
        _context.TransactionCategories.Add(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(
        CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        TransactionCategory category,
        CancellationToken ct = default)
    {
        _context.TransactionCategories.Remove(category);
        await _context.SaveChangesAsync(ct);
    }
}
