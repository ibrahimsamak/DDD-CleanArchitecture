
using OrderFlow.Application.Common.Events;
using OrderFlow.Domain.Orders.Events;

namespace OrderFlow.Application.Orders.EventHandlers;

public sealed class OrderPlacedLogHandler() : DomainEventHandler<OrderPlacedDomainEvent>
{
    protected override Task HandleAsync(OrderPlacedDomainEvent domainEvent, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
