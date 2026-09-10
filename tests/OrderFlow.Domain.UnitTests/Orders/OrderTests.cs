namespace OrderFlow.Domain.UnitTests.Orders;

using FluentAssertions;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Orders.Events;
using OrderFlow.Domain.Orders.ValueObjects;
using Xunit;

/// <summary>
/// Nothing here is mocked, hosted or configured — these tests instantiate domain types and nothing
/// else. That is the proof the dependency rule holds.
/// </summary>
public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static Address Addr() => Address.Create("1 King St", "Toronto", "M5H 1A1", "CA");
    private static Order NewOrder() => Order.Create("cust-1", Addr(), "CAD", Now);

    [Fact]
    public void Place_WithLines_SetsStatusAndRaisesEvent()
    {
        var order = NewOrder();
        order.AddLine("SKU-1", 2, 10m);

        order.Place(Now);

        order.Status.Should().Be(OrderStatus.Placed);
        order.Total.Amount.Should().Be(20m);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedDomainEvent);
    }

    [Fact]
    public void Place_WithNoLines_Throws()
    {
        var act = () => NewOrder().Place(Now);
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Order.Empty");
    }

    [Fact]
    public void Create_WithoutCustomerId_Throws()
    {
        var act = () => Order.Create("  ", Addr(), "CAD", Now);
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Order.Customer");
    }

    [Fact]
    public void AddLine_DuplicateSku_Throws()
    {
        var order = NewOrder();
        order.AddLine("SKU-1", 1, 5m);

        var act = () => order.AddLine("SKU-1", 1, 5m);
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Order.DuplicateSku");
    }

    [Fact]
    public void AddLine_AfterPlaced_Throws()
    {
        var order = NewOrder();
        order.AddLine("SKU-1", 1, 5m);
        order.Place(Now);

        var act = () => order.AddLine("SKU-2", 1, 5m);
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Order.Immutable");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddLine_WithNonPositiveQuantity_Throws(int quantity)
    {
        var act = () => NewOrder().AddLine("SKU-1", quantity, 5m);
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("OrderLine.Quantity");
    }

    [Fact]
    public void Cancel_IsIdempotent_AndRaisesOneEvent()
    {
        var order = NewOrder();
        order.AddLine("SKU-1", 1, 5m);
        order.Place(Now);
        order.ClearDomainEvents();

        order.Cancel("customer request", Now);
        order.Cancel("again", Now);

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledDomainEvent);
    }

    [Fact]
    public void Total_SumsLines()
    {
        var order = NewOrder();
        order.AddLine("SKU-1", 2, 10.50m);
        order.AddLine("SKU-2", 3, 1.00m);

        order.Total.Amount.Should().Be(24.00m);
        order.Total.Currency.Should().Be("CAD");
    }

    [Fact]
    public void Total_OfAnEmptyOrder_IsZeroInTheOrderCurrency()
    {
        var total = NewOrder().Total;

        total.Amount.Should().Be(0m);
        total.Currency.Should().Be("CAD");
    }

    [Fact]
    public void Lines_CannotBeMutatedFromOutside()
    {
        var order = NewOrder();
        order.Lines.Should().BeAssignableTo<IReadOnlyList<OrderLine>>();
        order.Lines.Should().NotBeOfType<List<OrderLine>>("callers must not be able to cast and Add");
    }

    [Fact]
    public void Money_DifferentCurrencies_CannotBeAdded()
    {
        var act = () => Money.Create(1, "CAD").Add(Money.Create(1, "USD"));
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Money.CurrencyMismatch");
    }

    [Fact]
    public void Money_IsComparedByValue()
    {
        Money.Create(10m, "CAD").Should().Be(Money.Create(10m, "CAD"));
        Money.Create(10m, "CAD").Should().NotBe(Money.Create(10m, "USD"));
    }

    [Fact]
    public void Money_RejectsNegativeAmounts()
    {
        var act = () => Money.Create(-0.01m, "CAD");
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Money.Negative");
    }

    [Theory]
    [InlineData("")]
    [InlineData("CA")]
    [InlineData("CADD")]
    public void Money_RequiresAThreeLetterCurrency(string currency)
    {
        var act = () => Money.Create(1m, currency);
        act.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Money.Currency");
    }

    [Fact]
    public void Address_IsComparedByValue_AndTrimsItsParts()
    {
        var a = Address.Create("  1 King St ", " Toronto", "M5H 1A1 ", " CA ");
        var b = Address.Create("1 King St", "Toronto", "M5H 1A1", "CA");

        a.Should().Be(b);
        a.Line1.Should().Be("1 King St");
    }

    [Fact]
    public void Address_RequiresLine1AndCountry()
    {
        var noLine1 = () => Address.Create(" ", "Toronto", "M5H 1A1", "CA");
        var noCountry = () => Address.Create("1 King St", "Toronto", "M5H 1A1", " ");

        noLine1.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Address.Line1");
        noCountry.Should().Throw<DomainException>().Which.Error.Code.Should().Be("Address.Country");
    }

    [Fact]
    public void Orders_AreComparedByIdentity_NotByValue()
    {
        var one = NewOrder();
        var another = NewOrder();

        one.Should().NotBe(another, "two orders with identical contents are still different orders");
        one.Should().Be(one);
    }
}
