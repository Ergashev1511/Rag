using System.Threading.Channels;
using Rag.Application.Abstractions;

namespace Rag.Infrastructure.Background;

/// <summary>Jarayon ichidagi navbat. Server qayta ishga tushsa IngestionWorker tugallanmagan versiyalarni DB dan qayta qo'yadi.</summary>
internal sealed class ChannelIngestionQueue : IIngestionQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(versionId, cancellationToken);

    public void EnqueueDelayed(Guid versionId, TimeSpan delay) =>
        _ = Task.Delay(delay).ContinueWith(_ => _channel.Writer.TryWrite(versionId), TaskScheduler.Default);
}
