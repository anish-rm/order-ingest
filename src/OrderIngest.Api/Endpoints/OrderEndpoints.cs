using OrderIngest.Api.Dtos;
using OrderIngest.Data;
using OrderIngest.Domain;

namespace OrderIngest.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders", async (OrderRepository repository, CancellationToken ct) =>
        {
            var orders = await repository.ListNewestFirstAsync(ct);
            return Results.Ok(orders.Select(ToSummary));
        });

        app.MapGet("/orders/{id:long}", async (long id, OrderRepository repository, CancellationToken ct) =>
        {
            var order = await repository.GetByIdAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(ToDetail(order));
        });
    }

    private static OrderSummaryDto ToSummary(Order order) => new(
        order.Id,
        order.Provider.ToString(),
        order.Status.ToString(),
        order.TotalCents,
        order.Currency,
        order.ReceivedAt);

    private static OrderDetailDto ToDetail(Order order) => new(
        order.Id,
        order.Provider.ToString(),
        order.ExternalOrderId,
        order.Status.ToString(),
        order.RawStatus,
        new CustomerDto(order.Customer.Name, order.Customer.Phone, order.Customer.Email),
        order.LineItems.Select(i => new LineItemDto(i.Name, i.Quantity, i.PriceCents)).ToList(),
        order.TotalCents,
        order.Currency,
        order.ReceivedAt,
        order.LastUpdatedAt);
}
