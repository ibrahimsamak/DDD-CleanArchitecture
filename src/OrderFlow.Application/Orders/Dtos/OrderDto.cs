namespace OrderFlow.Application.Orders.Dtos;

public sealed record OrderDto(
    Guid Id,
    string CustomerId,
    string Status,
    decimal Total,
    string Currency,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderLineDto> Lines);
