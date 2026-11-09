using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class FinancialAccountSharingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly FinancialAccountService _service;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();

    public FinancialAccountSharingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FinanceDbContext(options);
        _db.Database.EnsureCreated();
        _db.Users.AddRange(
            CreateUser(_ownerId, "owner@example.com"),
            CreateUser(_memberId, "member@example.com"));
        _db.SaveChanges();

        _service = new FinancialAccountService(
            new FinancialAccountRepository(_db),
            new HouseholdAuthorizationService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ShareAndUnshare_ChangesHouseholdAccountVisibilityImmediately()
    {
        var household = new Household
        {
            OwnerUserId = _ownerId,
            Name = "Home"
        };
        var account = new FinancialAccount
        {
            UserId = _ownerId,
            Name = "Checking",
            AccountType = "Chequing",
            Currency = "CAD"
        };
        _db.Households.Add(household);
        _db.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = _ownerId,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = _memberId,
                Role = HouseholdRole.Viewer
            });
        _db.FinancialAccounts.Add(account);
        await _db.SaveChangesAsync();

        var shared = await _service.ShareAsync(
            account.Id,
            _ownerId,
            household.Id);
        var memberAccounts = await _service.GetAllAsync(_memberId);

        Assert.Equal(household.Id, shared?.HouseholdId);
        Assert.False(Assert.Single(memberAccounts).IsOwner);

        var privateAccount = await _service.UnshareAsync(
            account.Id,
            _ownerId);
        memberAccounts = await _service.GetAllAsync(_memberId);

        Assert.Null(privateAccount?.HouseholdId);
        Assert.Empty(memberAccounts);
    }

    [Fact]
    public async Task Share_RejectsUserOutsideHousehold()
    {
        var household = new Household
        {
            OwnerUserId = _memberId,
            Name = "Home"
        };
        var account = new FinancialAccount
        {
            UserId = _ownerId,
            Name = "Checking",
            AccountType = "Chequing",
            Currency = "CAD"
        };
        _db.Households.Add(household);
        _db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = _memberId,
            Role = HouseholdRole.Owner
        });
        _db.FinancialAccounts.Add(account);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ShareAsync(account.Id, _ownerId, household.Id));
        Assert.Null((await _db.FinancialAccounts.FindAsync(account.Id))?.HouseholdId);
    }

    [Theory]
    [InlineData(HouseholdRole.Owner, true, true)]
    [InlineData(HouseholdRole.Admin, true, true)]
    [InlineData(HouseholdRole.Editor, true, false)]
    [InlineData(HouseholdRole.Viewer, false, false)]
    public async Task Authorization_EnforcesHouseholdRoleCapabilities(
        HouseholdRole role,
        bool canEdit,
        bool canManage)
    {
        var household = new Household
        {
            OwnerUserId = role == HouseholdRole.Owner
                ? _memberId
                : _ownerId,
            Name = "Home"
        };
        _db.Households.Add(household);
        if (role != HouseholdRole.Owner)
        {
            _db.HouseholdMembers.Add(new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = _ownerId,
                Role = HouseholdRole.Owner
            });
        }

        _db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = _memberId,
            Role = role
        });
        await _db.SaveChangesAsync();

        var authorization = new HouseholdAuthorizationService(_db);

        Assert.Equal(
            canEdit,
            await authorization.CanEditAsync(_memberId, household.Id));
        Assert.Equal(
            canManage,
            await authorization.CanManageAsync(_memberId, household.Id));
    }

    private static ApplicationUser CreateUser(Guid id, string email) => new()
    {
        Id = id,
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString()
    };
}
