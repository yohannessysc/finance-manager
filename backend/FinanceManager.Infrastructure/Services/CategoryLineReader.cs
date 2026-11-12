using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

// One categorized amount: a whole transaction, or one line of a split.
internal sealed record CategoryLine(
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    Guid? CategoryId,
    decimal Amount,
    DateTime DateUtc);

// Shared by budgets, the dashboard and reports so split transactions and
// subcategories are counted the same way everywhere.
internal static class CategoryLineReader
{
    public static async Task<List<CategoryLine>> ReadAsync(
        FinanceDbContext db,
        IQueryable<FinancialTransaction> transactions,
        CancellationToken ct)
    {
        var whole = await transactions
            .Where(t => !db.TransactionSplits.Any(s => s.TransactionId == t.Id))
            .Select(t => new
            {
                t.Id,
                t.FinancialAccountId,
                t.Type,
                t.CategoryId,
                t.Amount,
                t.TransactionDateUtc
            })
            .ToListAsync(ct);

        var splits = await (
            from split in db.TransactionSplits.AsNoTracking()
            join t in transactions on split.TransactionId equals t.Id
            select new
            {
                t.Id,
                t.FinancialAccountId,
                t.Type,
                split.CategoryId,
                split.Amount,
                t.TransactionDateUtc
            })
            .ToListAsync(ct);

        return whole
            .Select(x => new CategoryLine(
                x.Id, x.FinancialAccountId, x.Type, x.CategoryId, x.Amount, x.TransactionDateUtc))
            .Concat(splits.Select(x => new CategoryLine(
                x.Id, x.FinancialAccountId, x.Type, x.CategoryId, x.Amount, x.TransactionDateUtc)))
            .ToList();
    }

    // Category id -> (name, parent id) for the given ids and their parents.
    public static async Task<Dictionary<Guid, (string Name, Guid? ParentId)>> ReadCategoriesAsync(
        FinanceDbContext db,
        IEnumerable<Guid?> categoryIds,
        CancellationToken ct)
    {
        var ids = categoryIds
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var categories = await db.TransactionCategories
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.ParentCategoryId })
            .ToListAsync(ct);

        var parentIds = categories
            .Where(c => c.ParentCategoryId.HasValue && !ids.Contains(c.ParentCategoryId.Value))
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();

        var parents = await db.TransactionCategories
            .AsNoTracking()
            .Where(c => parentIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.ParentCategoryId })
            .ToListAsync(ct);

        return categories
            .Concat(parents)
            .ToDictionary(c => c.Id, c => (c.Name, c.ParentCategoryId));
    }
}
