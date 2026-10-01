using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderIngest.Domain;

namespace OrderIngest.Data;

public enum UpsertResult
{
    Inserted,
    Updated,
    Skipped,
}

public class OrderRepository(OrderIngestDbContext db, ILogger<OrderRepository> logger)
{
    /// <summary>
    /// Inserts the order, or — if a row for (provider, external order id)
    /// already exists — applies the incoming status only when its rank is
    /// strictly greater than the stored one. Replays and out-of-order
    /// events are skipped, so posting the same payload twice never
    /// duplicates or regresses a row.
    /// </summary>
    public async Task<UpsertResult> UpsertAsync(Order incoming, CancellationToken ct = default)
    {
        var existing = await db.Orders.FirstOrDefaultAsync(
            o => o.Provider == incoming.Provider && o.ExternalOrderId == incoming.ExternalOrderId,
            ct);

        if (existing is null)
        {
            db.Orders.Add(incoming);
            try
            {
                await db.SaveChangesAsync(ct);
                return UpsertResult.Inserted;
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                return UpsertResult.Skipped;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to insert {Provider} order {ExternalOrderId}",
                    incoming.Provider, incoming.ExternalOrderId);
                throw;
            }
        }

        // Monotonic guard: only a strictly higher-ranked status may advance the row.
        if (incoming.Status.Rank() <= existing.Status.Rank())
        {
            return UpsertResult.Skipped;
        }

        existing.Status = incoming.Status;
        existing.RawStatus = incoming.RawStatus;
        existing.RawPayload = incoming.RawPayload;
        existing.LastUpdatedAt = incoming.LastUpdatedAt;
        await db.SaveChangesAsync(ct);
        return UpsertResult.Updated;
    }

    public Task<List<Order>> ListNewestFirstAsync(CancellationToken ct = default) =>
        db.Orders.AsNoTracking()
            .OrderByDescending(o => o.ReceivedAt)
            .ToListAsync(ct);

    public Task<Order?> GetByIdAsync(long id, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
}
