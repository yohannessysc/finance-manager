using FinanceManager.Application.Interfaces;

namespace FinanceManager.Api.BackgroundServices;

/// <summary>
/// Periodically pulls new bank activity for every active connection, so paychecks,
/// purchases and balances show up without the user pressing "Sync".
/// Interval is configurable via "Plaid:SyncIntervalMinutes" (default 60).
/// In production, Plaid's SYNC_UPDATES_AVAILABLE webhook can trigger syncs sooner.
/// </summary>
public sealed class BankSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BankSyncBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public BankSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BankSyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = configuration.GetValue<int?>("Plaid:SyncIntervalMinutes") ?? 60;
        _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Bank sync background service started (every {Interval}).", _interval);

        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncService = scope.ServiceProvider.GetRequiredService<IBankSyncService>();
                    var logs = await syncService.SyncAllConnectionsAsync();
                    var imported = logs.Sum(l => l.TransactionsImported);
                    if (imported > 0)
                    {
                        _logger.LogInformation("Background bank sync imported {Count} transactions.", imported);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Background bank sync failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
    }
}
