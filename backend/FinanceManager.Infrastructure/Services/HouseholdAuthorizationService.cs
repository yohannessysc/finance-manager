using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class HouseholdAuthorizationService
    : IHouseholdAuthorizationService
{
    private readonly FinanceDbContext _db;

    public HouseholdAuthorizationService(FinanceDbContext db)
    {
        _db = db;
    }

    public Task<List<Guid>> GetHouseholdIdsAsync(
        Guid userId,
        CancellationToken ct = default) =>
        _db.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.HouseholdId)
            .ToListAsync(ct);

    public Task<bool> IsMemberAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default) =>
        _db.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId &&
                     x.HouseholdId == householdId,
                ct);

    public async Task<bool> CanEditAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default)
    {
        var role = await GetRoleAsync(userId, householdId, ct);
        return role is HouseholdRole.Owner or
            HouseholdRole.Admin or
            HouseholdRole.Editor;
    }

    public async Task<bool> CanManageAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default)
    {
        var role = await GetRoleAsync(userId, householdId, ct);
        return role is HouseholdRole.Owner or HouseholdRole.Admin;
    }

    public async Task<HouseholdRole?> GetRoleAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default) =>
        await _db.HouseholdMembers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.HouseholdId == householdId)
            .Select(x => (HouseholdRole?)x.Role)
            .SingleOrDefaultAsync(ct);
}
