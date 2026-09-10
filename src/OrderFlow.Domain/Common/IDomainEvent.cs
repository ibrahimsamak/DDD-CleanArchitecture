namespace OrderFlow.Domain.Common;


public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
