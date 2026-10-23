
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Dashboard;

public record UpcomingRecurringResponse(
    Guid Id,
    string Description,
    TransactionType Type,
    decimal Amount,
    string Currency,
    DateTime NextOccurrenceUtc,
    string TimeZoneId
);
