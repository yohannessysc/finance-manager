
using FinanceManager.Application.Interfaces;

namespace FinanceManager.Api.BackgroundServices;

public sealed class RecurringPostingBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringPostingBackgroundService> _logger;

    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(30);

    public RecurringPostingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringPostingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Recurring posting background service started.");

        // Process overdue transactions immediately on startup.
        await ProcessSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }

        _logger.LogInformation(
            "Recurring posting background service stopped.");
    }

    private async Task ProcessSafelyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            // Background services are singletons.
            // Create a scope for scoped services and DbContext.
            using var scope = _scopeFactory.CreateScope();

            var postingService =
                scope.ServiceProvider
                    .GetRequiredService<IRecurringPostingService>();

            var posted = await postingService.ProcessDueAsync(
                maxOccurrences: 50,
                ct: cancellationToken);

            if (posted > 0)
            {
                _logger.LogInformation(
                    "Recurring processor posted {Count} transactions.",
                    posted);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while processing recurring transactions.");
        }
    }
}
