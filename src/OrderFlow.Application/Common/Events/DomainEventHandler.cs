namespace OrderFlow.Application.Common.Events;

using OrderFlow.Domain.Common;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public abstract class DomainEventHandler<TEvent> : IDomainEventHandler where TEvent : IDomainEvent
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        domainEvent is TEvent typed ? HandleAsync(typed, ct) : Task.CompletedTask;

    protected abstract Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
