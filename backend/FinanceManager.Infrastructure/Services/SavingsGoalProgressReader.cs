using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

internal sealed record SavingsGoalProgress(
    decimal AmountSaved,
    decimal TargetAmount,
    decimal MonthlyPace,
    decimal? AverageMonthlyExpenses,
    string? LinkedAccountName);

// Shared by the goal API and the background automation so both
// agree on how much has been saved and what the target is.
internal sealed class SavingsGoalProgressReader
{
    private const string DefaultTimeZoneId = "America/Toronto";

    private readonly FinanceDbContext _db;

    public SavingsGoalProgressReader(FinanceDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<Guid, SavingsGoalProgress>> ReadAsync(
        IReadOnlyCollection<SavingsGoal> goals,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var lookbackStart = nowUtc.AddDays(
            -SavingsGoalProjectionCalculator.LookbackDays);

        var manualGoalIds = goals
            .Where(x => x.LinkedAccountId is null)
            .Select(x => x.Id)
            .ToList();

        var contributions = (await _db.SavingsGoalContributions
            .AsNoTracking()
            .Where(x => manualGoalIds.Contains(x.SavingsGoalId))
            .Select(x => new
            {
                x.SavingsGoalId,
                x.Amount,
                x.ContributionDateUtc
            })
            .ToListAsync(ct))
            .ToLookup(x => x.SavingsGoalId);

        var accountIds = goals
            .Where(x => x.LinkedAccountId is not null)
            .Select(x => x.LinkedAccountId!.Value)
            .Distinct()
            .ToList();

        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var flows = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(x =>
                x.TransactionDateUtc <= nowUtc &&
                (accountIds.Contains(x.FinancialAccountId) ||
                 (x.DestinationAccountId.HasValue &&
                  accountIds.Contains(x.DestinationAccountId.Value))))
            .Select(x => new AccountFlow(
                x.FinancialAccountId,
                x.DestinationAccountId,
                x.Type,
                x.Amount,
                x.TransactionDateUtc))
            .ToListAsync(ct);

        var results = new Dictionary<Guid, SavingsGoalProgress>(goals.Count);

        foreach (var goal in goals)
        {
            decimal saved;
            decimal pace;
            string? accountName = null;

            if (goal.LinkedAccountId is { } accountId &&
                accounts.TryGetValue(accountId, out var account))
            {
                var accountFlows = flows
                    .Where(x =>
                        x.SourceAccountId == accountId ||
                        x.DestinationAccountId == accountId)
                    .ToList();

                saved = account.OpeningBalance +
                        accountFlows.Sum(x => x.NetFor(accountId));

                var historyStart = accountFlows
                    .Select(x => x.DateUtc)
                    .Append(account.CreatedAtUtc)
                    .Min();
                var windowStart = Max(lookbackStart, historyStart);

                pace = Math.Max(0, SavingsGoalProjectionCalculator.MonthlyRate(
                    accountFlows
                        .Where(x => x.DateUtc >= windowStart)
                        .Sum(x => x.NetFor(accountId)),
                    windowStart,
                    nowUtc));

                accountName = account.Name;
            }
            else
            {
                var items = contributions[goal.Id].ToList();
                saved = items.Sum(x => x.Amount);

                var historyStart = items
                    .Select(x => x.ContributionDateUtc)
                    .Append(goal.CreatedAtUtc)
                    .Min();
                var windowStart = Max(lookbackStart, historyStart);

                pace = SavingsGoalProjectionCalculator.MonthlyRate(
                    items
                        .Where(x => x.ContributionDateUtc >= windowStart)
                        .Sum(x => x.Amount),
                    windowStart,
                    nowUtc);
            }

            // A running schedule is a commitment, so it sets
            // the minimum expected pace.
            if (goal is
                {
                    NextAutoContributionUtc: not null,
                    AutoContributionAmount: { } autoAmount,
                    AutoContributionFrequency: { } frequency
                })
            {
                pace = Math.Max(
                    pace,
                    SavingsGoalProjectionCalculator.MonthlyEquivalent(
                        autoAmount,
                        frequency));
            }

            decimal? averageExpenses = null;
            var target = goal.TargetAmount;

            if (goal is
                {
                    GoalType: SavingsGoalType.EmergencyFund,
                    EmergencyFundMonths: { } months
                })
            {
                averageExpenses = await GetAverageMonthlyExpensesAsync(
                    goal,
                    lookbackStart,
                    nowUtc,
                    ct);

                if (averageExpenses > 0)
                    target = Math.Round(
                        averageExpenses.Value * months,
                        2,
                        MidpointRounding.AwayFromZero);
            }

            results[goal.Id] = new SavingsGoalProgress(
                saved,
                target,
                pace,
                averageExpenses,
                accountName);
        }

        return results;
    }

    public async Task<string> GetTimeZoneIdAsync(
        Guid userId,
        CancellationToken ct)
    {
        return await _db.UserPreferences
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TimeZone)
            .SingleOrDefaultAsync(ct)
            ?? DefaultTimeZoneId;
    }

    // Spending of the people the fund protects: the household's
    // accounts for shared goals, otherwise the owner's accounts.
    private async Task<decimal?> GetAverageMonthlyExpensesAsync(
        SavingsGoal goal,
        DateTime lookbackStart,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var scopeAccountIds = (goal.HouseholdId is { } householdId
                ? _db.FinancialAccounts.Where(x => x.HouseholdId == householdId)
                : _db.FinancialAccounts.Where(x => x.UserId == goal.UserId))
            .Where(x => x.Currency == goal.Currency)
            .Select(x => x.Id);

        var expenses = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(x =>
                x.Type == TransactionType.Expense &&
                x.TransactionDateUtc >= lookbackStart &&
                x.TransactionDateUtc <= nowUtc &&
                scopeAccountIds.Contains(x.FinancialAccountId))
            .Select(x => new { x.Amount, x.TransactionDateUtc })
            .ToListAsync(ct);

        if (expenses.Count == 0)
            return null;

        return SavingsGoalProjectionCalculator.MonthlyRate(
            expenses.Sum(x => x.Amount),
            expenses.Min(x => x.TransactionDateUtc),
            nowUtc);
    }

    private static DateTime Max(DateTime left, DateTime right) =>
        left > right ? left : right;

    private sealed record AccountFlow(
        Guid SourceAccountId,
        Guid? DestinationAccountId,
        TransactionType Type,
        decimal Amount,
        DateTime DateUtc)
    {
        // Same rules as AccountBalanceService.
        public decimal NetFor(Guid accountId)
        {
            var net = 0m;

            if (SourceAccountId == accountId)
            {
                net += Type switch
                {
                    TransactionType.Income => Amount,
                    TransactionType.Expense => -Amount,
                    TransactionType.Transfer => -Amount,
                    _ => 0
                };
            }

            if (DestinationAccountId == accountId &&
                Type == TransactionType.Transfer)
            {
                net += Amount;
            }

            return net;
        }
    }
}
