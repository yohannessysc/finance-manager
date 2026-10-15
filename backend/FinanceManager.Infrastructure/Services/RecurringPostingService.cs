
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceManager.Infrastructure.Services;

public class RecurringPostingService : IRecurringPostingService
{
    private readonly FinanceDbContext _context;

    public RecurringPostingService(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<int> ProcessDueAsync(
        int maxOccurrences = 50,
        CancellationToken ct = default)
    {
        if (maxOccurrences < 1 || maxOccurrences > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOccurrences),
                "Batch size must be between 1 and 500.");
        }

        int posted = 0;

        // Capture a consistent processing cutoff.
        var nowUtc = DateTime.UtcNow;

        for (int i = 0; i < maxOccurrences; i++)
        {
            ct.ThrowIfCancellationRequested();

            // Each occurrence is committed independently.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    ct);

            // PostgreSQL locks the selected schedule.
            // SKIP LOCKED allows another worker to process
            // different schedules without waiting.
            var dueSchedules = await _context
                .RecurringTransactions
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "RecurringTransactions"
                    WHERE "Status" = {(int)RecurringStatus.Active}
                      AND "NextOccurrenceUtc" <= {nowUtc}
                    ORDER BY "NextOccurrenceUtc", "Id"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            var schedule = dueSchedules.FirstOrDefault();

            if (schedule is null)
            {
                await transaction.CommitAsync(ct);
                break;
            }

            var occurrenceUtc = schedule.NextOccurrenceUtc;

            // An expired schedule must not post payments
            // after its configured end date.
            if (schedule.EndDateUtc.HasValue &&
                occurrenceUtc > schedule.EndDateUtc.Value)
            {
                schedule.Status = RecurringStatus.Cancelled;

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _context.ChangeTracker.Clear();
                continue;
            }

            // Calculate the next occurrence using the
            // original start date and the user's time zone.
            var nextOccurrenceUtc =
                RecurrenceCalculator.GetNextOccurrence(
                    schedule.StartDateUtc,
                    schedule.Frequency,
                    occurrenceUtc,
                    schedule.TimeZoneId);

            // Extra protection if a matching occurrence
            // was already posted by a previous operation.
            var alreadyPosted = await _context
                .FinancialTransactions
                .AnyAsync(
                    x => x.RecurringTransactionId == schedule.Id &&
                         x.ScheduledOccurrenceUtc == occurrenceUtc,
                    ct);

            if (!alreadyPosted)
            {
                var financialTransaction =
                    new FinancialTransaction
                    {
                        UserId = schedule.UserId,
                        FinancialAccountId =
                            schedule.FinancialAccountId,
                        DestinationAccountId =
                            schedule.DestinationAccountId,
                        CategoryId = schedule.CategoryId,
                        RecurringTransactionId = schedule.Id,
                        ScheduledOccurrenceUtc = occurrenceUtc,
                        Type = schedule.Type,
                        Amount = schedule.Amount,
                        Description = schedule.Description,
                        Merchant = null,
                        TransactionDateUtc = occurrenceUtc,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                _context.FinancialTransactions.Add(
                    financialTransaction);
            }

            // Move the schedule forward inside the
            // same database transaction as the ledger insert.
            schedule.NextOccurrenceUtc = nextOccurrenceUtc;

            if (schedule.EndDateUtc.HasValue &&
                nextOccurrenceUtc > schedule.EndDateUtc.Value)
            {
                schedule.Status = RecurringStatus.Cancelled;
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            if (!alreadyPosted)
                posted++;

            // Prevent the DbContext from accumulating tracked
            // records during large catch-up batches.
            _context.ChangeTracker.Clear();
        }

        return posted;
    }
}
