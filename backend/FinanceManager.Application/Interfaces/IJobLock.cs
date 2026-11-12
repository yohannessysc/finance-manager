namespace FinanceManager.Application.Interfaces;

// Cluster-wide mutual exclusion for background jobs, so a job runs on
// at most one app instance at a time.
public interface IJobLock
{
    // Returns a lease to dispose when the job finishes, or null when
    // another instance already holds the lock (skip this run).
    Task<IAsyncDisposable?> TryAcquireAsync(
        string jobName,
        CancellationToken ct = default);
}
