namespace OrderIngest.Api.Dtos;

public record OrderSummaryDto(
    long Id,
    string Provider,
    string Status,
    long TotalCents,
    string Currency,
    DateTimeOffset ReceivedAt);

public record CustomerDto(string Name, string? Phone, string? Email);

public record LineItemDto(string Name, int Quantity, long PriceCents);

public record OrderDetailDto(
    long Id,
    string Provider,
    string ExternalOrderId,
    string Status,
    string RawStatus,
    CustomerDto Customer,
    IReadOnlyList<LineItemDto> LineItems,
    long TotalCents,
    string Currency,
    DateTimeOffset ReceivedAt,
    DateTimeOffset LastUpdatedAt);
