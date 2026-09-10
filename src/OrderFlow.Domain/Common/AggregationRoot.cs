namespace OrderFlow.Domain.Common;

public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id) where TId : notnull;
