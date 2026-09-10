namespace OrderFlow.Application.Common.Interfaces;

using OrderFlow.Domain.Common;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}
