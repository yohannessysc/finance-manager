namespace FinanceManager.Application.Interfaces;

// Hands bank connections to a background worker for syncing, so request
// handlers (e.g. Plaid webhooks) can reply immediately.
public interface IBankSyncQueue
{
    void Enqueue(Guid bankConnectionId);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
}
