using Microsoft.AspNetCore.Mvc;
using OrderIngest.Api.Dtos;
using OrderIngest.Data;
using OrderIngest.Domain;

namespace OrderIngest.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(OrderRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderSummaryDto>>> ListAsync(CancellationToken ct)
    {
        var orders = await repository.ListNewestFirstAsync(ct);
        return Ok(orders.Select(ToSummary));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDetailDto>> GetAsync(long id, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(ToDetail(order));
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
