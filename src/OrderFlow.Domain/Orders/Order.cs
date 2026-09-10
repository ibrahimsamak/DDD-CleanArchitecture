
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders.Events;
using OrderFlow.Domain.Orders.ValueObjects;

namespace OrderFlow.Domain.Orders;

public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = [];

    public Order(OrderId id) : base(id)
    {
    }

    private Order(OrderId id, string customerId, Address shippingAddress, string currency, DateTime createdAtUtc)
     : base(id)
    {
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Currency = currency;
        Status = OrderStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public string CustomerId { get; private set; } = default!;
    public Address ShippingAddress { get; private set; } = default!;
    public string Currency { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public Money Total =>
       _lines.Count == 0
           ? Money.Zero(Currency)
           : _lines.Select(l => l.LineTotal).Aggregate((a, b) => a + b);

    public static Order Create(string customerId, Address shippingAddress, string currency, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new DomainException(new CustomError("Order.Customer", "CustomerId is required."));
        }
        Money.Zero(currency);
        return new Order(OrderId.New(), customerId, shippingAddress, currency.ToUpperInvariant(), nowUtc);
    }
    public void AddLine(string sku, int quantity, decimal unitPrice)
    {
        EnsureMutable();

        if (_lines.Any(l => l.Sku == sku))
        {
            throw new DomainException(new CustomError("Order.DuplicateSku", $"SKU {sku} is already on the order."));
        }

        _lines.Add(new OrderLine(Guid.CreateVersion7(), sku, quantity, Money.Create(unitPrice, Currency)));
    }

    public void Place(DateTime nowUtc)
    {
        EnsureMutable();
        if (_lines.Count == 0)
        {
            throw new DomainException(new CustomError("Order.Empty", "Cannot place an order with no lines."));
        }

        Status = OrderStatus.Placed;
        Raise(new OrderPlacedDomainEvent(Id.Value, CustomerId, Total.Amount, Currency, nowUtc));
    }
    public void Cancel(string reason, DateTime nowUtc)
    {
        if (Status == OrderStatus.Cancelled)
        {
            return; 
        }

        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelledDomainEvent(Id.Value, reason, nowUtc));
    }

    private void EnsureMutable()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new DomainException(new CustomError("Order.Immutable", $"Order in status {Status} cannot be modified."));
        }
    }
}
