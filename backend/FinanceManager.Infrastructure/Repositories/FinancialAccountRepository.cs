
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class FinancialAccountRepository
    : IFinancialAccountRepository
{
    private readonly FinanceDbContext _context;

    public FinancialAccountRepository(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<FinancialAccount>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.FinancialAccounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<FinancialAccount?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.FinancialAccounts
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                ct);
    }

    public async Task AddAsync(
        FinancialAccount account,
        CancellationToken ct = default)
    {
        _context.FinancialAccounts.Add(account);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(
        FinancialAccount account,
        CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        FinancialAccount account,
        CancellationToken ct = default)
    {
        _context.FinancialAccounts.Remove(account);
        await _context.SaveChangesAsync(ct);
    }
}
