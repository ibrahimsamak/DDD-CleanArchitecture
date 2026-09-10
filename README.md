# OrderFlow

An order-management HTTP API built on .NET 10 with Clean Architecture and a
domain-driven core. Four projects, one dependency rule, no framework types in
the middle.

---

## Architecture

The solution is layered so that dependencies only ever point **inward**. The
domain sits at the centre and references nothing; every outer layer depends on
the layers inside it and never the reverse.

```
                    ┌─────────────────────────────┐
                    │        OrderFlow.Api        │   controllers, contracts,
                    │  HTTP • DI composition root │   exception handling
                    └──────────────┬──────────────┘
                                   │
              ┌────────────────────┴────────────────────┐
              │                                         │
              ▼                                         ▼
┌───────────────────────────┐          ┌───────────────────────────────┐
│  OrderFlow.Application    │◀─────────│   OrderFlow.Infrastructure    │
│  use cases • ports • DTOs │          │ EF Core • repositories • UoW  │
└─────────────┬─────────────┘          └───────────────────────────────┘
              │
              ▼
┌───────────────────────────┐
│     OrderFlow.Domain      │   aggregates • value objects • domain events
│   (zero dependencies)     │
└───────────────────────────┘
```

`Infrastructure` points *inward* at `Application`: it implements the interfaces
the application declares, and the application never learns that EF Core exists.
The API project is the only place that knows about all of them, and it knows
them only to wire them together.

### The layers

**`OrderFlow.Domain`** — the meaning of an order, expressed in plain C# with no
package references at all.

| Building block | What it is |
| --- | --- |
| `Entity<TId>` / `AggregateRoot<TId>` | identity-based equality, plus the domain-event buffer |
| `ValueObject` | structural equality derived from `GetEqualityComponents()` |
| `Order` | the aggregate root — owns its lines and guards its own invariants |
| `OrderLine` | an entity inside the aggregate, only constructible via `Order.AddLine` |
| `OrderId` | a `readonly record struct` over `Guid` — no bare GUIDs cross a signature |
| `Money`, `Address` | value objects that validate on creation and cannot be mutated |
| `OrderPlacedDomainEvent`, `OrderCancelledDomainEvent` | facts recorded by the aggregate |
| `IOrderRepository` | the write-side port, owned by the domain |
| `DomainException` / `CustomError` | rule violations carrying a stable machine-readable code |

State transitions live on the aggregate, not in a service: `Order.Create`,
`AddLine`, `Place` and `Cancel` each enforce their own preconditions, so an
order already `Placed` or `Cancelled` simply cannot be modified.

**`OrderFlow.Application`** — the use cases, and the ports they need.

`OrderService` (behind `IOrderService`) orchestrates: validate the input, build
the value objects, drive the aggregate, persist through the unit of work. It
takes its collaborators as interfaces — `IOrderRepository`, `IOrderReadStore`,
`IUnitOfWork`, `IValidator<T>`, `TimeProvider` — so the layer is testable with
no database and no clock.

Input is validated by FluentValidation (`PlaceOrderInputValidator`) before the
aggregate is touched; failures surface as a field-keyed `ValidationException`.
Missing rows surface as `NotFoundException`.

Reads and writes are deliberately split. Writes go through `IOrderRepository`
and return tracked aggregates. Reads go through `IOrderReadStore`, which
projects straight to `OrderDto` — no aggregate is ever materialised to answer a
query.

**`OrderFlow.Infrastructure`** — the adapters.

`OrderDbContext` maps the aggregate with EF Core: the strongly-typed `OrderId`
through a value converter, `OrderStatus` as a string (so reordering the enum
can never reinterpret existing rows), `Address` as an owned type inlined onto
the `Orders` table, and `Lines` as an owned collection in `OrderLines`. The
`Lines` navigation is configured for field access, so EF writes through
`_lines` and the invariants in `AddLine` can never be bypassed.
`Order.Version` is a SQL `rowversion`, giving optimistic concurrency for free.

`UnitOfWork` is where persistence and domain events meet:

1. harvest the events from every tracked aggregate and clear them, so a handler
   that triggers another save cannot re-dispatch them;
2. dispatch **before** `SaveChanges`, so anything a handler writes joins the
   same transaction and a throwing handler aborts the whole operation;
3. commit.

`DomainEventDispatcher` fans each event out to the registered
`IDomainEventHandler`s; `DomainEventHandler<TEvent>` gives each handler a typed
`HandleAsync` and quietly no-ops on events it does not care about.

**`OrderFlow.Api`** — the HTTP edge.

`OrdersController` is thin: bind a request contract, map it to an application
input, call `IOrderService`, return a status code. The API contracts
(`PlaceOrderRequest`, `CancelOrderRequest`) are kept separate from the
application DTOs, so the wire format can change without touching a use case.

`GlobalExceptionHandler` is the single place where exceptions become responses.
Every failure leaves as RFC 7807 `ProblemDetails`:

| Exception | Status | Response detail |
| --- | --- | --- |
| `ValidationException` | `400` | `errors` extension, keyed by field |
| `NotFoundException` | `404` | — |
| `DomainException` | `409` | `code` extension carrying the domain error code |
| `DbUpdateConcurrencyException` | `409` | concurrent update conflict |
| anything else | `500` | generic message; the details stay in the logs |

`Program.cs` is the composition root — `AddApplication()` and
`AddInfrastructure(configuration)` are called there and nowhere else. The
container runs with `ValidateOnBuild` and `ValidateScopes` enabled, so an
unregistered or misscoped dependency fails at startup rather than on the first
request that needs it.

### A request end to end

```
POST /api/orders
   │
   ├─ OrdersController            binds PlaceOrderRequest, maps to PlaceOrderInput
   ├─ OrderService                validates the input
   ├─ Address.Create / Order.Create
   ├─ Order.AddLine × n           rejects duplicate SKUs and non-positive quantities
   ├─ Order.Place                 raises OrderPlacedDomainEvent
   ├─ IOrderRepository.Add
   └─ IUnitOfWork.SaveChangesAsync
          ├─ dispatch the domain events to their handlers
          └─ commit
   │
   └─ 201 Created  →  Location: /api/orders/{id}
```

Anything that throws on the way out is caught by `GlobalExceptionHandler` and
shaped into a `ProblemDetails` payload.

---

## Getting started

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- SQL Server reachable from your machine (LocalDB, a container, or a real
  instance)
- `dotnet-ef`, for creating and applying migrations:

  ```bash
  dotnet tool install --global dotnet-ef
  ```

### 1. Clone and restore

```bash
git clone https://github.com/ibrahimsamak/DDD-CleanArchitecture.git
cd DDD-CleanArchitecture
dotnet restore OrderFlow.slnx
```

### 2. Point it at a database

The API reads the `OrderDb` connection string. Set it with user secrets so it
stays out of the repository:

```bash
dotnet user-secrets --project src/OrderFlow.Api init
dotnet user-secrets --project src/OrderFlow.Api set "ConnectionStrings:OrderDb" \
  "Server=localhost,1433;Database=OrderFlow;User Id=sa;Password=<your-password>;TrustServerCertificate=True"
```

An environment variable works just as well:

```bash
export ConnectionStrings__OrderDb="Server=localhost,1433;Database=OrderFlow;..."
```

If you need a database to talk to, one container is enough:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<your-password>" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

### 3. Create the schema

Migrations live in the infrastructure project and are run against the API
project, which owns the configuration:

```bash
dotnet ef migrations add InitialCreate \
  --project src/OrderFlow.Infrastructure \
  --startup-project src/OrderFlow.Api
```

In `Development` the API applies pending migrations on startup. To apply them
yourself:

```bash
dotnet ef database update \
  --project src/OrderFlow.Infrastructure \
  --startup-project src/OrderFlow.Api
```

### 4. Run

```bash
dotnet run --project src/OrderFlow.Api
```

| | |
| --- | --- |
| HTTP | <http://localhost:5173> |
| HTTPS | <https://localhost:7158> |
| Swagger UI | <http://localhost:5173/swagger> (Development only) |

### 5. Build and test

```bash
dotnet build OrderFlow.slnx
dotnet test  OrderFlow.slnx
```

Four test projects, each proving a different claim:

| Project | What it proves |
| --- | --- |
| `OrderFlow.ArchitectureTests` | the dependency rule holds — by reflection over the assemblies, not by assertion in a document |
| `OrderFlow.Domain.UnitTests` | the aggregate's invariants, with no mocks, containers or DI |
| `OrderFlow.Application.UnitTests` | the use cases, against substituted ports and a `FakeTimeProvider` |
| `OrderFlow.Api.IntegrationTests` | the endpoints end to end, against real SQL Server |

The integration tests start SQL Server in a container through Testcontainers,
so **Docker must be running** for them; the other three projects need nothing.
To run only those:

```bash
dotnet test tests/OrderFlow.Domain.UnitTests
dotnet test tests/OrderFlow.Application.UnitTests
dotnet test tests/OrderFlow.ArchitectureTests
```

---

## API

Base path: `/api/orders`. Everything is JSON.

| Method | Route | Purpose | Success |
| --- | --- | --- | --- |
| `POST` | `/api/orders` | Place a new order | `201 Created` |
| `GET` | `/api/orders/{id}` | Fetch one order | `200 OK` |
| `GET` | `/api/orders?page=&pageSize=&status=` | List orders, newest first | `200 OK` |
| `POST` | `/api/orders/{id}/cancel` | Cancel an order (idempotent) | `204 No Content` |

`page` defaults to `1`, `pageSize` to `20` and is clamped to `100`. `status`
accepts `Pending`, `Placed` or `Cancelled`, case-insensitively.

### Place an order

```bash
curl -X POST http://localhost:5173/api/orders \
  -H "Content-Type: application/json" \
  -d '{
        "customerId": "cust-1001",
        "currency": "USD",
        "addressLine1": "12 Bahariye Cd",
        "city": "Istanbul",
        "postalCode": "34710",
        "country": "TR",
        "lines": [
          { "sku": "KB-87", "quantity": 2, "unitPrice": 149.50 },
          { "sku": "MS-01", "quantity": 1, "unitPrice": 39.00 }
        ]
      }'
```

```json
{ "id": "0199a1c8-7f3e-7c21-9a44-2f9b1d5e77aa" }
```

### Read it back

```bash
curl http://localhost:5173/api/orders/0199a1c8-7f3e-7c21-9a44-2f9b1d5e77aa
```

```json
{
  "id": "0199a1c8-7f3e-7c21-9a44-2f9b1d5e77aa",
  "customerId": "cust-1001",
  "status": "Placed",
  "total": 338.00,
  "currency": "USD",
  "createdAtUtc": "2026-09-10T08:14:02.117Z",
  "lines": [
    { "sku": "KB-87", "quantity": 2, "unitPrice": 149.50, "lineTotal": 299.00 },
    { "sku": "MS-01", "quantity": 1, "unitPrice": 39.00, "lineTotal": 39.00 }
  ]
}
```

### Cancel it

```bash
curl -X POST http://localhost:5173/api/orders/0199a1c8-7f3e-7c21-9a44-2f9b1d5e77aa/cancel \
  -H "Content-Type: application/json" \
  -d '{ "reason": "Customer changed their mind" }'
```

Cancelling an already-cancelled order is a no-op and still returns `204`.

### When something is wrong

A rejected request comes back as `ProblemDetails`:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "Currency": [ "'Currency' must be 3 characters in length." ],
    "Lines": [ "'Lines' must not be empty." ]
  }
}
```

A broken business rule carries the domain error code instead:

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Domain rule violated",
  "status": 409,
  "detail": "SKU KB-87 is already on the order.",
  "code": "Order.DuplicateSku"
}
```

---

## Project layout

```
OrderFlow.slnx
Directory.Build.props              shared build settings for every project
docs/adr/                          architecture decision records
src/
  OrderFlow.Domain/
    Common/                        Entity, AggregateRoot, ValueObject, events, errors
    Orders/                        Order, OrderLine, OrderId, OrderStatus, IOrderRepository
      ValueObjects/                Money, Address
      Events/                      OrderPlaced, OrderCancelled
  OrderFlow.Application/
    Common/Interfaces/             IUnitOfWork, IOrderReadStore, IDomainEventDispatcher
    Common/Events/                 IDomainEventHandler, DomainEventHandler<T>
    Common/Exceptions/             NotFoundException, ValidationException
    Common/Models/                 PagedResult<T>
    Orders/                        IOrderService, OrderService, DTOs, validators, handlers
    DependencyInjection.cs         AddApplication()
  OrderFlow.Infrastructure/
    Persistence/                   OrderDbContext, UnitOfWork, configurations, repositories
    Events/                        DomainEventDispatcher
    DependencyInjection.cs         AddInfrastructure()
  OrderFlow.Api/
    Controllers/                   OrdersController
    Contracts/                     PlaceOrderRequest, CancelOrderRequest
    Infrastructure/                GlobalExceptionHandler
    Program.cs                     composition root
tests/
  OrderFlow.Domain.UnitTests/
  OrderFlow.Application.UnitTests/
  OrderFlow.Api.IntegrationTests/
  OrderFlow.ArchitectureTests/
```

`Directory.Build.props` applies to every project: `net10.0`, nullable reference
types on, implicit usings on, `TreatWarningsAsErrors`, and the
`latest-Recommended` analysis level. Warnings do not accumulate here.

---

## Decision records

The reasoning behind the structure is written down in `docs/adr/`:

- [0001 — Clean Architecture with an inward dependency rule](docs/adr/0001-clean-architecture.md)
- [0002 — Application services instead of a mediator library](docs/adr/0002-application-services-no-mediator.md)
- [0003 — Domain events dispatched in-process, inside the transaction](docs/adr/0003-in-process-domain-events.md)

---

## Stack

.NET 10 · ASP.NET Core · Entity Framework Core 10 (SQL Server) ·
FluentValidation · Swashbuckle
