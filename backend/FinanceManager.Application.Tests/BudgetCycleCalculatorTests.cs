using FinanceManager.Application.Services;

namespace FinanceManager.Application.Tests;

public sealed class BudgetCycleCalculatorTests
{
    [Fact]
    public void UsesCurrentCycleWhenLocalDateIsOnOrAfterStartDay()
    {
        var nowUtc = new DateTime(
            2027, 3, 15, 16, 0, 0, DateTimeKind.Utc);

        var period = BudgetCycleCalculator.GetCurrentPeriod(
            nowUtc,
            15,
            "America/Toronto");

        Assert.Equal(
            new DateTime(2027, 3, 15, 4, 0, 0, DateTimeKind.Utc),
            period.StartUtc);
        Assert.Equal(
            new DateTime(2027, 4, 15, 4, 0, 0, DateTimeKind.Utc),
            period.EndUtc);
    }

    [Fact]
    public void UsesPreviousCycleWhenLocalDateIsBeforeStartDay()
    {
        var nowUtc = new DateTime(
            2027, 3, 14, 12, 0, 0, DateTimeKind.Utc);

        var period = BudgetCycleCalculator.GetCurrentPeriod(
            nowUtc,
            15,
            "America/Toronto");

        Assert.Equal(
            new DateTime(2027, 2, 15, 5, 0, 0, DateTimeKind.Utc),
            period.StartUtc);
        Assert.Equal(
            new DateTime(2027, 3, 15, 4, 0, 0, DateTimeKind.Utc),
            period.EndUtc);
    }

    [Fact]
    public void RejectsInvalidCycleStartDay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetCycleCalculator.GetCurrentPeriod(
                DateTime.UtcNow,
                29,
                "UTC"));
    }
}
