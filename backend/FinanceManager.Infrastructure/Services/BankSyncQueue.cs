using System.Threading.Channels;

using FinanceManager.Application.Interfaces;

namespace FinanceManager.Infrastructure.Services;

// In-process queue. A sync lost on restart is harmless: the scheduled
// sync covers every connection anyway.
public sealed class BankSyncQueue : IBankSyncQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

    public void Enqueue(Guid bankConnectionId) =>
        _channel.Writer.TryWrite(bankConnectionId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
