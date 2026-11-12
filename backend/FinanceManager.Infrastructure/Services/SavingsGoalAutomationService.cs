using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

public sealed class SavingsGoalAutomationService : ISavingsGoalAutomationService
{
    private const int GoalBatchSize = 100;

    // Caps catch-up after downtime so one goal cannot stall the batch.
    private const int MaximumOccurrencesPerGoal = 50;

    private readonly FinanceDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<SavingsGoalAutomationService> _logger;
    private readonly SavingsGoalProgressReader _progress;

    public SavingsGoalAutomationService(
        FinanceDbContext db,
        IEmailService email,
        ILogger<SavingsGoalAutomationService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
        _progress = new SavingsGoalProgressReader(db);
    }

    public async Task<int> ProcessScheduledContributionsAsync(
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;

        var goals = await _db.SavingsGoals
            .Where(x =>
                x.NextAutoContributionUtc != null &&
                x.NextAutoContributionUtc <= nowUtc &&
                x.LinkedAccountId == null &&
                x.AutoContributionAmount != null &&
                x.AutoContributionFrequency != null &&
                x.AutoContributionStartUtc != null)
            .OrderBy(x => x.NextAutoContributionUtc)
            .Take(GoalBatchSize)
            .ToListAsync(ct);

        if (goals.Count == 0)
            return 0;

        var progress = await _progress.ReadAsync(goals, nowUtc, ct);
        var posted = 0;

        foreach (var goal in goals)
        {
            ct.ThrowIfCancellationRequested();

            var remaining =
                progress[goal.Id].TargetAmount - progress[goal.Id].AmountSaved;
            var timeZoneId = await _progress.GetTimeZoneIdAsync(goal.UserId, ct);
            var added = new List<SavingsGoalContribution>();

            while (goal.NextAutoContributionUtc is { } occurrence &&
                   occurrence <= nowUtc &&
                   added.Count < MaximumOccurrencesPerGoal)
            {
                // A reached goal ends its schedule instead of over-saving.
                if (remaining <= 0)
                {
                    goal.NextAutoContributionUtc = null;
                    break;
                }

                var amount = Math.Min(goal.AutoContributionAmount!.Value, remaining);
                added.Add(new SavingsGoalContribution
                {
                    UserId = goal.UserId,
                    SavingsGoalId = goal.Id,
                    Amount = amount,
                    ContributionDateUtc = occurrence,
                    Note = "Scheduled contribution",
                    ScheduledOccurrenceUtc = occurrence,
                    CreatedAtUtc = nowUtc
                });

                remaining -= amount;
                goal.NextAutoContributionUtc = remaining <= 0
                    ? null
                    : RecurrenceCalculator.GetNextOccurrence(
                        goal.AutoContributionStartUtc!.Value,
                        goal.AutoContributionFrequency!.Value,
                        occurrence,
                        timeZoneId);
            }

            goal.UpdatedAtUtc = nowUtc;
            _db.SavingsGoalContributions.AddRange(added);

            try
            {
                await _db.SaveChangesAsync(ct);
                posted += added.Count;
            }
            catch (DbUpdateException ex)
            {
                // The unique (goal, occurrence) index rejected a duplicate,
                // e.g. another instance posted it first. Skip this goal;
                // it is re-read on the next run.
                _logger.LogWarning(
                    ex,
                    "Scheduled contributions for savings goal {GoalId} were not saved.",
                    goal.Id);

                foreach (var contribution in added)
                    _db.Entry(contribution).State = EntityState.Detached;
                _db.Entry(goal).State = EntityState.Detached;
            }
        }

        return posted;
    }

    public async Task<int> SendMilestoneNotificationsAsync(
        CancellationToken ct = default)
    {
        var goals = await _db.SavingsGoals
            .Where(x => x.NotificationsEnabled && x.LastMilestoneNotified < 100)
            .ToListAsync(ct);

        if (goals.Count == 0)
            return 0;

        var progress = await _progress.ReadAsync(goals, DateTime.UtcNow, ct);
        var notified = 0;

        foreach (var goal in goals)
        {
            var milestone = SavingsGoalProjectionCalculator.MilestoneFor(
                progress[goal.Id].AmountSaved,
                progress[goal.Id].TargetAmount);

            if (milestone <= goal.LastMilestoneNotified)
                continue;

            foreach (var email in await GetRecipientEmailsAsync(goal, ct))
            {
                try
                {
                    await _email.SendSavingsGoalMilestoneAsync(
                        email,
                        goal.Name,
                        milestone,
                        ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Recorded as sent anyway: retrying would re-email the
                    // members who did receive it.
                    _logger.LogError(
                        ex,
                        "Milestone email for savings goal {GoalId} failed.",
                        goal.Id);
                }
            }

            goal.LastMilestoneNotified = milestone;
            await _db.SaveChangesAsync(ct);
            notified++;
        }

        return notified;
    }

    private async Task<List<string>> GetRecipientEmailsAsync(
        SavingsGoal goal,
        CancellationToken ct)
    {
        var recipientIds = goal.HouseholdId is { } householdId
            ? _db.HouseholdMembers
                .Where(x => x.HouseholdId == householdId)
                .Select(x => x.UserId)
            : _db.Users
                .Where(x => x.Id == goal.UserId)
                .Select(x => x.Id);

        return await _db.Users
            .AsNoTracking()
            .Where(x =>
                recipientIds.Contains(x.Id) &&
                x.EmailConfirmed &&
                x.Email != null)
            .Select(x => x.Email!)
            .ToListAsync(ct);
    }
}
