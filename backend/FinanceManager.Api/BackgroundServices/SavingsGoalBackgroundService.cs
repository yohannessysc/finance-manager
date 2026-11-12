using FinanceManager.Application.Interfaces;

namespace FinanceManager.Api.BackgroundServices;

public sealed class SavingsGoalBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SavingsGoalBackgroundService> _logger;

    public SavingsGoalBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SavingsGoalBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Savings goal background service started (every {Interval}).",
            CheckInterval);

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
    }

    private async Task ProcessSafelyAsync(
        CancellationToken cancellationToken)
    {
        // With several app instances only one runs the jobs per tick, so
        // milestone emails are never sent twice.
        IAsyncDisposable? lease;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            lease = await scope.ServiceProvider
                .GetRequiredService<IJobLock>()
                .TryAcquireAsync("savings-goals", cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Could not acquire the savings goal job lock.");
            return;
        }

        if (lease is null)
            return;

        await using var _ = lease;

        // Contributions run first so milestones they cross are
        // announced in the same pass. Each step fails independently.
        await RunStepAsync(
            "posting scheduled savings contributions",
            async automation =>
            {
                var posted = await automation
                    .ProcessScheduledContributionsAsync(cancellationToken);
                if (posted > 0)
                    _logger.LogInformation(
                        "Posted {Count} scheduled savings contributions.",
                        posted);
            },
            cancellationToken);

        await RunStepAsync(
            "sending savings goal milestone notifications",
            async automation =>
            {
                var notified = await automation
                    .SendMilestoneNotificationsAsync(cancellationToken);
                if (notified > 0)
                    _logger.LogInformation(
                        "Sent milestone notifications for {Count} savings goals.",
                        notified);
            },
            cancellationToken);
    }

    private async Task RunStepAsync(
        string description,
        Func<ISavingsGoalAutomationService, Task> step,
        CancellationToken cancellationToken)
    {
        try
        {
            // Background services are singletons; DbContext is scoped.
            using var scope = _scopeFactory.CreateScope();
            await step(scope.ServiceProvider
                .GetRequiredService<ISavingsGoalAutomationService>());
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while {Step}.", description);
        }
    }
}
