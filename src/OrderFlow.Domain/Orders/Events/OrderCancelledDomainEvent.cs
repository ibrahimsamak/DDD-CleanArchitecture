namespace OrderFlow.Domain.Orders.Events;

using OrderFlow.Domain.Common;

public sealed record OrderCancelledDomainEvent(
    Guid OrderId,
    string Reason,
    DateTime OccurredOnUtc) : IDomainEvent;
