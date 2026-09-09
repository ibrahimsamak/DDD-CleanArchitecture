# 0003. Domain events dispatched in-process, inside the transaction

- Status: Accepted
- Date: 2026-09-09

## Context

The `Order` aggregate raises `OrderPlacedDomainEvent` and
`OrderCancelledDomainEvent`. Something must consume them. In Week 2 they become
integration events published to Kafka via a Transactional Outbox, but there is
no second service yet, and building an outbox with nothing to publish to is
speculative work.

## Decision

A hand-rolled dispatcher (~30 lines) resolves `IDomainEventHandler`
implementations and invokes them. `UnitOfWork.SaveChangesAsync` collects events
from tracked aggregates, clears them, dispatches them, and *then* calls
`DbContext.SaveChangesAsync`.

Dispatching **before** the save means any database work a handler performs is
tracked by the same `DbContext` and committed in the same transaction. A
handler that throws rolls the whole operation back.

Domain events are deliberately **not** integration events. They carry domain
meaning, use domain types, and stay in-process. The translation to a versioned
cross-service contract happens at the boundary — which is exactly where the
outbox lands in Week 2.

## Alternatives considered

- **MediatR `INotification` + `IPublisher`.** The usual answer. Rejected for the
  reasons in ADR 0002; the hand-rolled version is 30 lines and has no magic.
- **Dispatch after commit.** Simpler to reason about, but a handler failure
  leaves the system inconsistent with no rollback. Rejected.
- **Build the outbox now.** Deferred to Week 2, where a broker and a consumer
  justify it.

## Consequences

- (+) Domain events are a working feature, not a dead field on the entity.
- (+) The collection point in `UnitOfWork` is precisely where Week 2 inserts the
  outbox writer. The change is localized to one method.
- (-) Handlers run synchronously inside the request, so a slow handler slows the
  write. Acceptable while handlers only log.
- (-) No retry, no replay, no durability. That is the whole point of the outbox,
  and the reason Week 2 exists.
