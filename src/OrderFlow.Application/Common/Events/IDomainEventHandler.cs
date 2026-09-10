namespace OrderFlow.Application.Common.Events;

using OrderFlow.Domain.Common;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public interface IDomainEventHandler
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct);
}
