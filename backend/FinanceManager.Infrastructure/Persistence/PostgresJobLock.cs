using System.Security.Cryptography;
using System.Text;

using FinanceManager.Application.Interfaces;

using Microsoft.Extensions.Configuration;

using Npgsql;

namespace FinanceManager.Infrastructure.Persistence;

// Session-level Postgres advisory lock held on a dedicated connection.
// If the instance dies, the connection drops and Postgres releases the
// lock, so a crashed worker can never block the job permanently.
public sealed class PostgresJobLock : IJobLock
{
    private readonly string _connectionString;

    public PostgresJobLock(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string jobName,
        CancellationToken ct = default)
    {
        var key = KeyFor(jobName);
        var connection = new NpgsqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(ct);

            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(@key)",
                connection);
            command.Parameters.AddWithValue("key", key);

            if (await command.ExecuteScalarAsync(ct) is true)
                return new Lease(connection, key);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    // Stable across processes and restarts (string.GetHashCode is not).
    private static long KeyFor(string jobName) =>
        BitConverter.ToInt64(
            SHA256.HashData(Encoding.UTF8.GetBytes("FinanceManager.Job:" + jobName)),
            0);

    private sealed class Lease(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(@key)",
                    connection);
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteScalarAsync();
            }
            finally
            {
                // Closing the session releases the lock even if unlock failed.
                await connection.DisposeAsync();
            }
        }
    }
}
