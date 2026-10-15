
using FinanceManager.Application.Services;
using FinanceManager.Domain.Enums;
using Xunit;

namespace FinanceManager.Application.Tests;

public class RecurrenceCalculatorTests
{
    private const string Toronto = "America/Toronto";

    [Fact]
    public void MonthlySchedule_PreservesLocalTimeAcrossDST()
    {
        // January 1 at 9 AM EST.
        var start = new DateTime(
            2027, 1, 1, 14, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceCalculator.GetOccurrence(
            start,
            RecurrenceFrequency.Monthly,
            6,
            Toronto);

        // July 1 at 9 AM EDT.
        Assert.Equal(
            new DateTime(2027, 7, 1, 13, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void MonthlySchedule_PreservesMonthEnd()
    {
        var start = new DateTime(
            2027, 1, 31, 14, 0, 0, DateTimeKind.Utc);

        var february = RecurrenceCalculator.GetOccurrence(
            start,
            RecurrenceFrequency.Monthly,
            1,
            Toronto);

        var march = RecurrenceCalculator.GetOccurrence(
            start,
            RecurrenceFrequency.Monthly,
            2,
            Toronto);

        Assert.Equal(
            new DateTime(2027, 2, 28, 14, 0, 0, DateTimeKind.Utc),
            february);

        Assert.Equal(
            new DateTime(2027, 3, 31, 13, 0, 0, DateTimeKind.Utc),
            march);
    }

    [Fact]
    public void NextOccurrence_SkipsPastOccurrences()
    {
        var start = new DateTime(
            2027, 1, 1, 14, 0, 0, DateTimeKind.Utc);

        var after = new DateTime(
            2027, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceCalculator.GetNextOccurrence(
            start,
            RecurrenceFrequency.Monthly,
            after,
            Toronto);

        Assert.Equal(
            new DateTime(2027, 4, 1, 13, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void SpringForward_MovesNonexistentTimeForward()
    {
        // March 7, 2027, 2:30 AM EST.
        var start = new DateTime(
            2027, 3, 7, 7, 30, 0, DateTimeKind.Utc);

        var result = RecurrenceCalculator.GetOccurrence(
            start,
            RecurrenceFrequency.Weekly,
            1,
            Toronto);

        // March 14, 2:30 AM does not exist.
        // Our policy moves it to 3:00 AM EDT.
        Assert.Equal(
            new DateTime(2027, 3, 14, 7, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void FallBack_ChoosesSecondAmbiguousTime()
    {
        // October 31, 2027, 1:30 AM EDT.
        var start = new DateTime(
            2027, 10, 31, 5, 30, 0, DateTimeKind.Utc);

        var result = RecurrenceCalculator.GetOccurrence(
            start,
            RecurrenceFrequency.Weekly,
            1,
            Toronto);

        // November 7, 1:30 AM occurs twice.
        // Our policy chooses the second occurrence (EST).
        Assert.Equal(
            new DateTime(2027, 11, 7, 6, 30, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void BiweeklySchedule_PreservesLocalTimeAcrossDST()
    {
        // March 1, 2027, 9:00 AM EST.
        var start = new DateTime(
            2027, 3, 1, 14, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceCalculator.GetOccurrence(
            start,
            RecurrenceFrequency.Biweekly,
            1,
            Toronto);

        // March 15, 2027, 9:00 AM EDT.
        Assert.Equal(
            new DateTime(2027, 3, 15, 13, 0, 0, DateTimeKind.Utc),
            result);
    }
}
