
namespace FinanceManager.Application.Interfaces;

public interface IRecurringPostingService
{
    Task<int> ProcessDueAsync(
        int maxOccurrences = 50,
        CancellationToken ct = default);
}
