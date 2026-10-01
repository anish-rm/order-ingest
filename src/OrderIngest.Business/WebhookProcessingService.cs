using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderIngest.Business.Mapping;
using OrderIngest.Business.Uber;
using OrderIngest.Data;
using OrderIngest.Domain;

namespace OrderIngest.Business;

/// <summary>
/// Consumes queued webhooks after the endpoint has already returned 200.
/// For Uber the full order is fetched via IUberOrderClient (the webhook
/// body only carries resource_href); DoorDash embeds the order directly.
/// Transient failures are retried up to 3 times; malformed payloads are
/// not retried (they can never succeed).
/// </summary>
public class WebhookProcessingService(
    WebhookQueue queue,
    IUberOrderClient uberOrderClient,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookProcessingService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessWithRetriesAsync(item, stoppingToken);
        }
    }

    private async Task ProcessWithRetriesAsync(WebhookWorkItem item, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await ProcessAsync(item, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Discarding malformed {Provider} payload", item.Provider);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex,
                    "Processing {Provider} webhook failed (attempt {Attempt}/{Max}), retrying",
                    item.Provider, attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
            catch (Exception ex)
            {
                // In production this would go to a dead-letter queue.
                logger.LogError(ex,
                    "Giving up on {Provider} webhook after {Max} attempts",
                    item.Provider, MaxAttempts);
                return;
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

        // DbContext is scoped; a BackgroundService is a singleton, so each
        // work item gets its own scope.
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<OrderRepository>();
        var result = await repository.UpsertAsync(order, ct);

        logger.LogInformation(
            "{Provider} order {ExternalOrderId}: {Result} (status {Status})",
            order.Provider, order.ExternalOrderId, result, order.Status);
    }
}
