using Microsoft.EntityFrameworkCore;
using OrderIngest.Domain;

namespace OrderIngest.Data;

public class OrderIngestDbContext(DbContextOptions<OrderIngestDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();

        order.ToTable("orders");
        order.HasKey(o => o.Id);

        order.Property(o => o.Provider).HasConversion<string>();
        order.Property(o => o.Status).HasConversion<string>();

        // SQLite cannot order or index DateTimeOffset directly, so store
        // both timestamps as UTC ticks (INTEGER).
        order.Property(o => o.ReceivedAt).HasConversion(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));
        order.Property(o => o.LastUpdatedAt).HasConversion(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

        order.HasIndex(o => new { o.Provider, o.ExternalOrderId }).IsUnique();

        // The admin list sorts newest-first.
        order.HasIndex(o => o.ReceivedAt);

        // Customer and line items are JSON columns on the single order row.
        order.OwnsOne(o => o.Customer, customer => customer.ToJson());
        order.OwnsMany(o => o.LineItems, item => item.ToJson());
    }
}
