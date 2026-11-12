using FinanceManager.Application.Interfaces;

namespace FinanceManager.Api.BackgroundServices;

// Runs syncs requested through IBankSyncQueue (e.g. by Plaid webhooks),
// one at a time, outside the request that asked for them.
public sealed class BankSyncQueueWorker : BackgroundService
{
    private readonly IBankSyncQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BankSyncQueueWorker> _logger;

    public BankSyncQueueWorker(
        IBankSyncQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BankSyncQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var connectionId in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider
                        .GetRequiredService<IBankSyncService>()
                        .SyncTransactionsAsync(connectionId);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Queued sync for connection {ConnectionId} failed.", connectionId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
