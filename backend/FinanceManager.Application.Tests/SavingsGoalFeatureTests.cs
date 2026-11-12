using FinanceManager.Application.DTOs.SavingsGoals;
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace FinanceManager.Application.Tests;

public sealed class SavingsGoalFeatureTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly Mock<IEmailService> _email = new();
    private readonly SavingsGoalService _service;
    private readonly SavingsGoalAutomationService _automation;
    private readonly Guid _ownerId;
    private readonly FinancialAccount _account;

    public SavingsGoalFeatureTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();

        _ownerId = AddUser("owner@example.com");
        _account = new FinancialAccount
        {
            UserId = _ownerId,
            Name = "High interest savings",
            Currency = "CAD",
            OpeningBalance = 500
        };
        _db.FinancialAccounts.Add(_account);
        _db.SaveChanges();

        _service = new SavingsGoalService(_db, new HouseholdAuthorizationService(_db));
        _automation = new SavingsGoalAutomationService(
            _db,
            _email.Object,
            NullLogger<SavingsGoalAutomationService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void ProjectionReportsStatusAndMonthlyAmountNeeded()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var deadline = now.AddMonths(10);

        var onTrack = SavingsGoalProjectionCalculator.Project(1000, 0, 200, deadline, now);
        Assert.Equal(SavingsGoalStatus.OnTrack, onTrack.Status);
        Assert.NotNull(onTrack.ProjectedCompletionDateUtc);
        Assert.InRange(onTrack.MonthlyContributionNeeded!.Value, 99m, 101m);

        var behind = SavingsGoalProjectionCalculator.Project(1000, 0, 50, deadline, now);
        Assert.Equal(SavingsGoalStatus.Behind, behind.Status);

        var noPace = SavingsGoalProjectionCalculator.Project(1000, 0, 0, deadline, now);
        Assert.Equal(SavingsGoalStatus.Behind, noPace.Status);
        Assert.Null(noPace.ProjectedCompletionDateUtc);

        var done = SavingsGoalProjectionCalculator.Project(1000, 1200, 0, deadline, now);
        Assert.Equal(SavingsGoalStatus.Completed, done.Status);
        Assert.Null(done.MonthlyContributionNeeded);

        Assert.Equal(
            SavingsGoalStatus.NoTargetDate,
            SavingsGoalProjectionCalculator.Project(1000, 0, 100, null, now).Status);
    }

    [Fact]
    public async Task LinkedGoalTracksAccountBalance()
    {
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            UserId = _ownerId,
            FinancialAccountId = _account.Id,
            Type = TransactionType.Income,
            Amount = 300,
            Description = "Deposit",
            TransactionDateUtc = DateTime.UtcNow.AddDays(-1)
        });
        await _db.SaveChangesAsync();

        var goal = await _service.CreateAsync(_ownerId, new SavingsGoalUpsertRequest(
            "House",
            "CAD",
            1000,
            null,
            LinkedAccountId: _account.Id));

        Assert.Equal(800, goal.AmountSaved);
        Assert.Equal("High interest savings", goal.LinkedAccountName);
        Assert.True(goal.MonthlyPace > 0);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddContributionAsync(goal.Id, _ownerId,
                new SavingsGoalContributionRequest(10, DateTime.UtcNow, null)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(_ownerId, new SavingsGoalUpsertRequest(
                "Second goal on same account",
                "CAD",
                1000,
                null,
                LinkedAccountId: _account.Id)));
    }

    [Fact]
    public async Task ScheduledContributionsPostUntilGoalIsReached()
    {
        var start = DateTime.UtcNow.Date.AddDays(-21).AddHours(12);
        var created = await _service.CreateAsync(_ownerId, new SavingsGoalUpsertRequest(
            "Trip",
            "CAD",
            250,
            null,
            AutoContributionAmount: 100,
            AutoContributionFrequency: RecurrenceFrequency.Weekly,
            AutoContributionStartUtc: start));

        // Past start dates only anchor the schedule; simulate downtime
        // since the first occurrence to exercise catch-up.
        var goal = await _db.SavingsGoals.SingleAsync(x => x.Id == created.Id);
        Assert.True(goal.NextAutoContributionUtc > DateTime.UtcNow);
        goal.NextAutoContributionUtc = start;
        await _db.SaveChangesAsync();

        Assert.Equal(3, await _automation.ProcessScheduledContributionsAsync());
        Assert.Equal(0, await _automation.ProcessScheduledContributionsAsync());

        var amounts = await _db.SavingsGoalContributions
            .Where(x => x.SavingsGoalId == goal.Id)
            .OrderBy(x => x.ContributionDateUtc)
            .Select(x => x.Amount)
            .ToListAsync();
        Assert.Equal([100m, 100m, 50m], amounts);

        var reloaded = Assert.Single(await _service.GetAllAsync(_ownerId));
        Assert.Equal(SavingsGoalStatus.Completed, reloaded.Status);
        Assert.Null(reloaded.NextAutoContributionUtc);
    }

    [Fact]
    public async Task HouseholdMembersShareGoalsByRole()
    {
        var editorId = AddUser("editor@example.com");
        var viewerId = AddUser("viewer@example.com");
        var household = new Household { OwnerUserId = _ownerId, Name = "Home" };
        _db.Households.Add(household);
        _db.HouseholdMembers.AddRange(
            new HouseholdMember { HouseholdId = household.Id, UserId = _ownerId, Role = HouseholdRole.Owner },
            new HouseholdMember { HouseholdId = household.Id, UserId = editorId, Role = HouseholdRole.Editor },
            new HouseholdMember { HouseholdId = household.Id, UserId = viewerId, Role = HouseholdRole.Viewer });
        await _db.SaveChangesAsync();

        var goal = await _service.CreateAsync(_ownerId, new SavingsGoalUpsertRequest(
            "Family car",
            "CAD",
            5000,
            null,
            HouseholdId: household.Id));

        await _service.AddContributionAsync(goal.Id, editorId,
            new SavingsGoalContributionRequest(200, DateTime.UtcNow, null));

        var viewerGoal = Assert.Single(await _service.GetAllAsync(viewerId));
        Assert.False(viewerGoal.CanEdit);
        Assert.Equal(200, viewerGoal.AmountSaved);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.AddContributionAsync(goal.Id, viewerId,
                new SavingsGoalContributionRequest(10, DateTime.UtcNow, null)));

        var contribution = Assert.Single(
            (await _service.GetContributionsAsync(goal.Id, viewerId))!);
        Assert.Equal("editor@example.com", contribution.ContributorEmail);
    }

    [Fact]
    public async Task MilestoneNotificationsAreSentOncePerMilestone()
    {
        var notified = await _service.CreateAsync(_ownerId,
            new SavingsGoalUpsertRequest("Laptop", "CAD", 100, null));
        var muted = await _service.CreateAsync(_ownerId,
            new SavingsGoalUpsertRequest("Muted", "CAD", 100, null,
                NotificationsEnabled: false));

        foreach (var id in new[] { notified.Id, muted.Id })
            await _service.AddContributionAsync(id, _ownerId,
                new SavingsGoalContributionRequest(60, DateTime.UtcNow, null));

        Assert.Equal(1, await _automation.SendMilestoneNotificationsAsync());
        Assert.Equal(0, await _automation.SendMilestoneNotificationsAsync());

        _email.Verify(x => x.SendSavingsGoalMilestoneAsync(
            "owner@example.com", "Laptop", 50, It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(x => x.SendSavingsGoalMilestoneAsync(
            It.IsAny<string>(), "Muted", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(await _db.SavingsGoals.AsNoTracking()
            .Where(x => x.Id == muted.Id)
            .Select(x => x.NotificationsEnabled)
            .SingleAsync());
    }

    [Fact]
    public async Task EmergencyFundTargetFollowsAverageSpending()
    {
        var goal = await _service.CreateAsync(_ownerId, new SavingsGoalUpsertRequest(
            "Emergency fund",
            "CAD",
            1000,
            null,
            GoalType: SavingsGoalType.EmergencyFund,
            EmergencyFundMonths: 3));

        // No spending history yet: the entered target is the fallback.
        Assert.Equal(1000, goal.TargetAmount);
        Assert.Null(goal.AverageMonthlyExpenses);

        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            UserId = _ownerId,
            FinancialAccountId = _account.Id,
            Type = TransactionType.Expense,
            Amount = 1200,
            Description = "Rent",
            TransactionDateUtc = DateTime.UtcNow.AddDays(-10)
        });
        await _db.SaveChangesAsync();

        var reloaded = Assert.Single(await _service.GetAllAsync(_ownerId));
        Assert.Equal(1200, reloaded.AverageMonthlyExpenses);
        Assert.Equal(3600, reloaded.TargetAmount);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(_ownerId, new SavingsGoalUpsertRequest(
                "Bad fund", "CAD", 1000, null,
                GoalType: SavingsGoalType.EmergencyFund,
                EmergencyFundMonths: 0)));
    }

    private Guid AddUser(string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user.Id;
    }
}
