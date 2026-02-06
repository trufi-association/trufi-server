using Gateway.Shared.Models;
using System.Threading.Channels;

namespace Gateway.Shared.Services;

/// <summary>
/// High-performance, non-blocking queue for logging requests asynchronously.
/// Uses System.Threading.Channels for efficient async operations.
/// </summary>
public class RequestLogQueue
{
    private readonly Channel<CreateRequestDto> _queue;

    public RequestLogQueue(int capacity = 10000)
    {
        // Bounded channel with capacity to prevent OutOfMemory
        var options = new BoundedChannelOptions(capacity)
        {
            // Drop oldest items if queue is full (prevents blocking)
            FullMode = BoundedChannelFullMode.DropOldest
        };

        _queue = Channel.CreateBounded<CreateRequestDto>(options);
    }

    /// <summary>
    /// Enqueues a request DTO for async processing.
    /// Returns immediately (< 1ms), never blocks.
    /// </summary>
    public ValueTask<bool> EnqueueAsync(CreateRequestDto dto, CancellationToken cancellationToken = default)
    {
        // TryWrite is non-blocking and returns immediately
        var written = _queue.Writer.TryWrite(dto);
        return ValueTask.FromResult(written);
    }

    /// <summary>
    /// Returns an async enumerable for consuming all items from the queue.
    /// Used by background worker to process items in batches.
    /// </summary>
    public IAsyncEnumerable<CreateRequestDto> DequeueAllAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the current number of items in the queue.
    /// Useful for monitoring/metrics.
    /// </summary>
    public int Count => _queue.Reader.Count;
}
