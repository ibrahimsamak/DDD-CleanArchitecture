namespace OrderFlow.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Domain.Common;

public sealed class UnitOfWork(OrderDbContext db, IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // 1. Harvest events from every tracked aggregate and clear them, so a
        //    handler that triggers another SaveChanges cannot re-dispatch them.
        var entities = db.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        // 2. Dispatch BEFORE the save, so anything a handler writes joins this
        //    same transaction. A throwing handler aborts the whole operation.
        if (events.Count > 0)
        {
            await dispatcher.DispatchAsync(events, ct);
        }

        // 3. Commit. EF wraps SaveChanges in a transaction on its own.
        return await db.SaveChangesAsync(ct);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();   // retries transient SQL errors
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await action();
            await SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
