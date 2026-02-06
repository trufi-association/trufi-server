using Gateway.Shared.Data;
using Gateway.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Gateway.Shared.Services;

/// <summary>
/// Background service that consumes requests from the queue and writes them to the database in batches.
/// This prevents blocking the request pipeline and provides massive performance improvements.
/// </summary>
public class RequestLogWriter : BackgroundService
{
    private readonly RequestLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RequestLogWriter> _logger;

    // Batching configuration
    private const int BatchSize = 100;
    private const int FlushIntervalMs = 1000; // Flush every second

    public RequestLogWriter(
        RequestLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<RequestLogWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RequestLogWriter background service started");

        try
        {
            await foreach (var batch in GetBatchesAsync(stoppingToken))
            {
                await WriteBatchToDatabaseAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RequestLogWriter background service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in RequestLogWriter background service");
            throw;
        }
    }

    private async IAsyncEnumerable<List<CreateRequestDto>> GetBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batch = new List<CreateRequestDto>();
        var lastFlushTime = DateTime.UtcNow;

        await foreach (var item in _queue.DequeueAllAsync(cancellationToken))
        {
            batch.Add(item);

            var shouldFlush = batch.Count >= BatchSize ||
                              (DateTime.UtcNow - lastFlushTime).TotalMilliseconds >= FlushIntervalMs;

            if (shouldFlush && batch.Count > 0)
            {
                yield return batch;
                batch = new List<CreateRequestDto>();
                lastFlushTime = DateTime.UtcNow;
            }
        }

        // Flush remaining items when stopping
        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private async Task WriteBatchToDatabaseAsync(List<CreateRequestDto> batch, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

            // Convert DTOs to entities
            var requests = batch.Select(dto => new Request
            {
                Method = dto.Method,
                Uri = dto.Uri,
                Host = dto.Host,
                Ip = dto.Ip,
                DeviceId = dto.DeviceId,
                UserAgent = dto.UserAgent,
                RequestContentType = dto.RequestContentType,
                RequestHeaders = dto.RequestHeaders,
                Body = dto.Body,
                StatusCode = dto.StatusCode,
                ResponseContentType = dto.ResponseContentType,
                ResponseHeaders = dto.ResponseHeaders,
                ResponseBody = dto.ResponseBody,
                ReceivedAt = DateTime.UtcNow
            }).ToList();

            // Batch insert - much more efficient than individual inserts
            context.Requests.AddRange(requests);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Successfully wrote batch of {Count} requests to database", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch of {Count} requests to database. Requests will be lost.", batch.Count);
            // Note: We don't rethrow here to prevent the background service from stopping
            // In production, you might want to implement a dead-letter queue or retry mechanism
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RequestLogWriter is stopping, flushing remaining items in queue (count: {Count})", _queue.Count);
        await base.StopAsync(cancellationToken);
    }
}
