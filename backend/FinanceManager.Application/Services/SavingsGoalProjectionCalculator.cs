using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Services;

public sealed record SavingsGoalProjection(
    SavingsGoalStatus Status,
    decimal? MonthlyContributionNeeded,
    DateTime? ProjectedCompletionDateUtc);

public static class SavingsGoalProjectionCalculator
{
    // How far back saving pace and average spending are measured.
    public const int LookbackDays = 90;

    private const decimal DaysPerMonth = 30.436875m;

    // Projections further out than this are not meaningful.
    private const decimal MaximumProjectionMonths = 1200;

    private static readonly int[] Milestones = [100, 75, 50, 25];

    public static SavingsGoalProjection Project(
        decimal targetAmount,
        decimal amountSaved,
        decimal monthlyPace,
        DateTime? targetDateUtc,
        DateTime nowUtc)
    {
        var remaining = Math.Max(0, targetAmount - amountSaved);

        if (remaining == 0)
            return new SavingsGoalProjection(
                SavingsGoalStatus.Completed,
                null,
                null);

        decimal? monthlyNeeded = null;
        if (targetDateUtc is { } due)
        {
            var monthsLeft = MonthsBetween(nowUtc, due);

            // Round up to the cent so following the advice
            // actually reaches the target.
            monthlyNeeded = monthsLeft <= 1
                ? remaining
                : Math.Ceiling(remaining / monthsLeft * 100) / 100;
        }

        DateTime? projected = null;
        if (monthlyPace > 0)
        {
            var monthsToGo = remaining / monthlyPace;

            if (monthsToGo <= MaximumProjectionMonths)
                projected = nowUtc.AddDays(
                    (double)(monthsToGo * DaysPerMonth));
        }

        var status = targetDateUtc switch
        {
            null => SavingsGoalStatus.NoTargetDate,
            var deadline when projected <= deadline => SavingsGoalStatus.OnTrack,
            _ => SavingsGoalStatus.Behind
        };

        return new SavingsGoalProjection(status, monthlyNeeded, projected);
    }

    // Average per month of an amount accumulated since windowStartUtc.
    // Windows shorter than a month count as one month so a single
    // recent deposit is not extrapolated into a huge pace.
    public static decimal MonthlyRate(
        decimal amount,
        DateTime windowStartUtc,
        DateTime nowUtc)
    {
        var months = Math.Max(1, MonthsBetween(windowStartUtc, nowUtc));
        return Math.Round(
            amount / months,
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal MonthlyEquivalent(
        decimal amount,
        RecurrenceFrequency frequency)
    {
        var perMonth = frequency switch
        {
            RecurrenceFrequency.Daily => amount * 365 / 12,
            RecurrenceFrequency.Weekly => amount * 52 / 12,
            RecurrenceFrequency.Biweekly => amount * 26 / 12,
            RecurrenceFrequency.Monthly => amount,
            RecurrenceFrequency.Yearly => amount / 12,
            _ => throw new ArgumentException(
                "Unsupported recurrence frequency.")
        };

        return Math.Round(perMonth, 2, MidpointRounding.AwayFromZero);
    }

    // Highest milestone (0, 25, 50, 75 or 100) reached.
    public static int MilestoneFor(decimal amountSaved, decimal targetAmount)
    {
        if (targetAmount <= 0)
            return 0;

        var percent = amountSaved / targetAmount * 100;
        return Milestones.FirstOrDefault(x => percent >= x);
    }

    private static decimal MonthsBetween(DateTime fromUtc, DateTime toUtc)
    {
        return (decimal)(toUtc - fromUtc).TotalDays / DaysPerMonth;
    }
}
