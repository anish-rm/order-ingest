using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderIngest.Business.Mapping;
using OrderIngest.Business.Retry;
using OrderIngest.Business.Uber;
using OrderIngest.Data;
using OrderIngest.Domain;

namespace OrderIngest.Business;

/// <summary>
/// Consumes queued webhooks after the endpoint has already returned 200.
/// For Uber the full order is fetched via IUberOrderClient (the webhook
/// body only carries resource_href); DoorDash embeds the order directly.
/// Transient failures are retried via <see cref="RetryExecutor"/>;
/// unprocessable payloads are discarded without retry (they can never
/// succeed).
/// </summary>
public class WebhookProcessingService(
    WebhookQueue queue,
    IUberOrderClient uberOrderClient,
    RetryExecutor retryExecutor,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookProcessingService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    private static bool IsTransientFailure(Exception ex) =>
        ex is DbUpdateException
            or SqliteException
            or HttpRequestException
            or IOException
            or TimeoutException;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On shutdown, stop accepting new items and drain the ones already
        // acknowledged with a 200 instead of dropping them. 
        using var drainOnShutdown = stoppingToken.Register(queue.Complete);

        await foreach (var item in queue.ReadAllAsync(CancellationToken.None))
        {
            using var scope = logger.BeginScope(
                "webhook {Provider}/{CorrelationId}", item.Provider, item.CorrelationId);
            try
            {
                await retryExecutor.ExecuteAsync(
                    ct => ProcessAsync(item, ct),
                    operationName: $"{item.Provider} webhook {item.CorrelationId}",
                    maxAttempts: MaxAttempts,
                    isRetryable: IsTransientFailure);
            }
            catch (RetryExhaustedException ex)
            {
                // In production this would go to a dead-letter queue.
                logger.LogError(ex,
                    "Giving up on {Provider} webhook {CorrelationId} after {Attempts} attempts",
                    item.Provider, item.CorrelationId, ex.TotalAttempts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Discarding unprocessable {Provider} payload {CorrelationId}",
                    item.Provider, item.CorrelationId);
            }
        }
    }

    private async Task ProcessAsync(WebhookWorkItem item, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var order = item.Provider switch
        {
            OrderProvider.Uber => UberOrderMapper.Map(
                await uberOrderClient.GetOrderJsonAsync(
                    UberOrderMapper.GetResourceHref(item.RawBody), ct),
                now),
            OrderProvider.DoorDash => DoorDashOrderMapper.Map(item.RawBody, now),
            _ => throw new InvalidOperationException($"Unexpected provider {item.Provider}"),
        };

        // DbContext is scoped; a BackgroundService is a singleton, so each work item gets its own scope.
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<OrderRepository>();
        var result = await repository.UpsertAsync(order, ct);

        logger.LogInformation(
            "{Provider} order {ExternalOrderId}: {Result} (status {Status})",
            order.Provider, order.ExternalOrderId, result, order.Status);
    }
}
