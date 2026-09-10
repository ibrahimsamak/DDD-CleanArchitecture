namespace OrderFlow.Infrastructure.Events;

using OrderFlow.Application.Common.Events;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Domain.Common;

public sealed class DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            foreach (var handler in handlers)
            {
                await handler.HandleAsync(domainEvent, ct);   // non-matching handlers no-op
            }
        }
    }
}
