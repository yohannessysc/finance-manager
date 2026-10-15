
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Services;

public static class RecurrenceCalculator
{
    public static DateTime GetOccurrence(
        DateTime startDateUtc,
        RecurrenceFrequency frequency,
        long occurrenceIndex,
        string timeZoneId)
    {
        ValidateUtc(startDateUtc);
        ValidateFrequency(frequency);

        if (occurrenceIndex < 0)
            throw new ArgumentOutOfRangeException(
                nameof(occurrenceIndex));

        var zone = GetTimeZone(timeZoneId);

        var localStart = DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(startDateUtc, zone),
            DateTimeKind.Unspecified);

        // Preserve the exact original start instant.
        if (occurrenceIndex == 0)
            return startDateUtc;

        DateTime localOccurrence = frequency switch
        {
            RecurrenceFrequency.Daily =>
                localStart.AddDays(occurrenceIndex),

            RecurrenceFrequency.Weekly =>
                localStart.AddDays(
                    checked(occurrenceIndex * 7)),

            RecurrenceFrequency.Biweekly =>
                localStart.AddDays(
                    checked(occurrenceIndex * 14)),

            RecurrenceFrequency.Monthly =>
                localStart.AddMonths(
                    checked((int)occurrenceIndex)),

            RecurrenceFrequency.Yearly =>
                localStart.AddYears(
                    checked((int)occurrenceIndex)),

            _ => throw new ArgumentException(
                "Unsupported recurrence frequency.")
        };

        return ConvertLocalToUtc(localOccurrence, zone);
    }

    public static DateTime GetNextOccurrence(
        DateTime startDateUtc,
        RecurrenceFrequency frequency,
        DateTime afterUtc,
        string timeZoneId)
    {
        ValidateUtc(startDateUtc);
        ValidateUtc(afterUtc);
        ValidateFrequency(frequency);

        if (afterUtc < startDateUtc)
            return startDateUtc;

        var zone = GetTimeZone(timeZoneId);

        var localStart =
            TimeZoneInfo.ConvertTimeFromUtc(startDateUtc, zone);

        var localAfter =
            TimeZoneInfo.ConvertTimeFromUtc(afterUtc, zone);

        long differenceInDays =
            (localAfter.Date - localStart.Date).Days;

        long high = frequency switch
        {
            RecurrenceFrequency.Daily =>
                differenceInDays + 3,

            RecurrenceFrequency.Weekly =>
                differenceInDays / 7 + 3,

            RecurrenceFrequency.Biweekly =>
                differenceInDays / 14 + 3,

            RecurrenceFrequency.Monthly =>
                (localAfter.Year - localStart.Year) * 12L
                + localAfter.Month - localStart.Month + 3,

            RecurrenceFrequency.Yearly =>
                localAfter.Year - localStart.Year + 3L,

            _ => throw new ArgumentException(
                "Unsupported recurrence frequency.")
        };

        long low = 0;

        // Find the first occurrence strictly after afterUtc.
        while (low < high)
        {
            long mid = low + (high - low) / 2;

            var candidate = GetOccurrence(
                startDateUtc,
                frequency,
                mid,
                timeZoneId);

            if (candidate <= afterUtc)
                low = mid + 1;
            else
                high = mid;
        }

        return GetOccurrence(
            startDateUtc,
            frequency,
            low,
            timeZoneId);
    }

    public static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new ArgumentException(
                "A time zone is required.");

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException(
                "Unknown time zone.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException(
                "Invalid time zone.");
        }
    }

    private static DateTime ConvertLocalToUtc(
        DateTime localDateTime,
        TimeZoneInfo zone)
    {
        localDateTime = DateTime.SpecifyKind(
            localDateTime,
            DateTimeKind.Unspecified);

        // Spring forward:
        // Move nonexistent local times to the first valid minute.
        while (zone.IsInvalidTime(localDateTime))
        {
            localDateTime = localDateTime.AddMinutes(1);
        }

        // Fall back:
        // Choose the second occurrence of an ambiguous time.
        if (zone.IsAmbiguousTime(localDateTime))
        {
            var offsets =
                zone.GetAmbiguousTimeOffsets(localDateTime);

            return new DateTimeOffset(
                localDateTime,
                offsets.Min()).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(
            localDateTime,
            zone);
    }

    private static void ValidateUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "Date must use UTC.");
    }

    private static void ValidateFrequency(
        RecurrenceFrequency frequency)
    {
        if (!Enum.IsDefined(frequency))
            throw new ArgumentException(
                "Invalid recurrence frequency.");
    }
}
