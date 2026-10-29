using System.Threading.Channels;

namespace Snare.Library.Ocr;

/// <summary>Captures waiting to have their text read. Duplicates are dropped while one is still queued.</summary>
public sealed class OcrQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly HashSet<Guid> _pending = [];
    private readonly Lock _gate = new();

    public void Enqueue(Guid id)
    {
        lock (_gate)
        {
            if (!_pending.Add(id))
            {
                return;
            }
        }

        _channel.Writer.TryWrite(id);
    }

    public async IAsyncEnumerable<Guid> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            lock (_gate)
            {
                _pending.Remove(id);
            }

            yield return id;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }
}
