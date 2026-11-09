namespace FinanceManager.Application.Services;

public static class BudgetCycleCalculator
{
    public static (DateTime StartUtc, DateTime EndUtc) GetCurrentPeriod(
        DateTime nowUtc,
        int startDay,
        string timeZoneId)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The current time must be UTC.", nameof(nowUtc));

        if (startDay is < 1 or > 28)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startDay),
                "Budget cycle start day must be between 1 and 28.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var startLocal = new DateTime(
            localNow.Year,
            localNow.Month,
            startDay,
            0,
            0,
            0,
            DateTimeKind.Unspecified);

        if (localNow.Date < startLocal)
            startLocal = startLocal.AddMonths(-1);

        var endLocal = startLocal.AddMonths(1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone));
    }
}
