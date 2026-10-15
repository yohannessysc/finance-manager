
using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Interfaces;

public interface IRecurringTransactionRepository
{
    Task<List<RecurringTransaction>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<RecurringTransaction?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task AddAsync(
        RecurringTransaction transaction,
        CancellationToken ct = default);

    Task SaveChangesAsync(
        CancellationToken ct = default);

    Task DeleteAsync(
        RecurringTransaction transaction,
        CancellationToken ct = default);

    Task<DateTime?> GetLastPostedOccurrenceAsync(
        Guid scheduleId,
        Guid userId,
        CancellationToken ct = default);
}
