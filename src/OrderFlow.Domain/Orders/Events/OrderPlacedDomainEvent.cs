namespace OrderFlow.Domain.Orders.Events;

using OrderFlow.Domain.Common;

public sealed record OrderPlacedDomainEvent(
    Guid OrderId,
    string CustomerId,
    decimal Total,
    string Currency,
    DateTime OccurredOnUtc) : IDomainEvent;
