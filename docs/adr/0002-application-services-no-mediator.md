# 0002. Application services instead of a mediator library

- Status: Accepted
- Date: 2026-09-09

## Context

The dominant .NET Clean Architecture templates use MediatR: each use case is an
`IRequest` record plus an `IRequestHandler`, and cross-cutting concerns are
`IPipelineBehavior` implementations. This service has four use cases, all known
at compile time, each with exactly one caller.

## Decision

One application service per aggregate — `IOrderService` / `OrderService` — with
one method per use case. Controllers inject it directly.

- **Validation:** FluentValidation validators injected into the service and
  invoked explicitly at the top of the method that needs them.
- **Transactions:** `IUnitOfWork.SaveChangesAsync` for single-aggregate writes
  (EF Core already wraps `SaveChanges` in a transaction);
  `ExecuteInTransactionAsync` only where more than one save must be atomic.
- **Logging:** `ILogger<OrderService>`, called where it says something useful.

## Alternatives considered

- **MediatR.** The de facto standard, and worth knowing. Rejected here because
  its core value is *runtime indirection* — a caller that does not know its
  callee — and nothing in this service needs that. We would be taking a
  dependency (commercially licensed from v13) to obtain a validation decorator.
- **Hand-rolled `ICommand`/`IQuery` handlers with Scrutor decorators.** Keeps
  one class per use case and makes the transaction boundary type-enforced, at
  the cost of ~9 extra files. A good choice at 20+ use cases. Overkill at four.

## Consequences

- (+) Wiring is compile-time checked. A missing registration fails at startup
  (with `ValidateOnBuild`), not on the first request to one endpoint.
- (+) "Go to definition" from the controller lands on the actual code.
- (+) No reflection, no runtime handler lookup, no licensing question.
- (-) `OrderService` will grow. The refactor trigger is explicit: **when it
  exceeds ~200 lines or gains a second reason to change, split it by use case.**
- (-) Cross-cutting concerns are repeated per method rather than applied once.
  With four methods that is a few duplicated lines; at twenty it would not be.

## Revisit when

In-process event fan-out is needed (one message, N handlers), or the use-case
count passes roughly fifteen.
