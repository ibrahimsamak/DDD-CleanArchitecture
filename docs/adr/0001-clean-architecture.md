# 0001. Adopt Clean Architecture with an inward dependency rule

- Status: Accepted
- Date: 2026-09-09

## Context

`OrderFlow` starts as one service but will be split into four in Week 2 and
deployed to AKS in Week 3. The parts most likely to change are the ones closest
to infrastructure: the database provider, the transport, the hosting model. The
part least likely to change is the meaning of an order.

## Decision

Four projects with a strictly inward dependency graph:

    Api -> Application -> Domain
    Infrastructure -> Application, Domain

`Domain` references no projects and no NuGet packages. Every outward need is
declared as an interface owned by an inner layer and implemented outward:

- `IOrderRepository` is declared in `Domain` (it deals in aggregates).
- `IUnitOfWork`, `IOrderReadStore`, `IDomainEventDispatcher` are declared in
  `Application` (they deal in application concerns and DTOs).

`Api` references `Infrastructure` for one reason only: `Program.cs` is the
composition root and must be able to call `AddInfrastructure()`. No other file
in `Api` may reference an `Infrastructure` type.

## Alternatives considered

- **Traditional N-tier (Api -> Business -> Data).** Rejected: the dependency
  points outward toward the database, so the domain ends up shaped by the schema.
- **Vertical slice architecture.** A strong fit for CRUD-heavy services, and it
  removes layer ceremony. Rejected here because the point of Week 1 is a
  defensible layered boundary that four services will copy in Week 2.

## Consequences

- (+) The domain is unit-testable with no fakes, no containers, no DI.
- (+) Week 3 swaps SQL Server for Azure SQL without touching a single line of
  `Domain` or `Application`.
- (-) More projects and more interfaces than a small service strictly needs.
- (-) The rule is not enforced by the compiler in the `Api -> Infrastructure`
  direction, so it is enforced by an architecture test instead (see Day 1).
