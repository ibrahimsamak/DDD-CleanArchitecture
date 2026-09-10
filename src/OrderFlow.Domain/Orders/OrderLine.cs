
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders.ValueObjects;

namespace OrderFlow.Domain.Orders;

public sealed class OrderLine : Entity<Guid>
{
    public OrderLine(Guid id) : base(id)
    {
    }

    internal OrderLine(Guid id, string sku, int quantity, Money unitPrice): base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException(new CustomError("OrderLine.Sku", "SKU is required."));
        }

        if (quantity <= 0)
        {
            throw new DomainException(new CustomError("OrderLine.Quantity", "Quantity must be positive."));
        }

        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string Sku { get; private set; } = default!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = default!;

    public Money LineTotal => UnitPrice.Multiply(Quantity);

}
