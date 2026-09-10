namespace OrderFlow.Application.UnitTests.Orders;

using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using OrderFlow.Application.Common.Exceptions;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Application.Orders.Validation;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Orders.ValueObjects;
using Xunit;

/// <summary>
/// Every port is substituted; the validator is the real one, because its rules are part of what the
/// service is being tested for.
/// </summary>
public sealed class OrderServiceTests
{
    private readonly IOrderRepository _repo = Substitute.For<IOrderRepository>();
    private readonly IOrderReadStore _readStore = Substitute.For<IOrderReadStore>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    private OrderService Sut() => new(_repo, _readStore, _uow, new PlaceOrderInputValidator(), _time);

    private static PlaceOrderInput ValidInput() => new(
        "cust-1", "CAD", "1 King St", "Toronto", "M5H 1A1", "CA",
        [new PlaceOrderLineInput("SKU-1", 2, 10m)]);

    private static Order PlacedOrder(DateTime now)
    {
        var order = Order.Create(
            "cust-1", Address.Create("1 King St", "Toronto", "M5H 1A1", "CA"), "CAD", now);
        order.AddLine("SKU-1", 2, 10m);
        order.Place(now);
        return order;
    }

    [Fact]
    public async Task PlaceOrder_AddsAggregateAndCommitsOnce()
    {
        var id = await Sut().PlaceOrderAsync(ValidInput(), CancellationToken.None);

        id.Should().NotBeEmpty();
        _repo.Received(1).Add(Arg.Any<Order>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_StampsTheInjectedClock()
    {
        Order? captured = null;
        _repo.When(r => r.Add(Arg.Any<Order>())).Do(c => captured = c.Arg<Order>());

        await Sut().PlaceOrderAsync(ValidInput(), CancellationToken.None);

        captured!.CreatedAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task PlaceOrder_BuildsTheAggregateFromTheInput()
    {
        Order? captured = null;
        _repo.When(r => r.Add(Arg.Any<Order>())).Do(c => captured = c.Arg<Order>());

        await Sut().PlaceOrderAsync(ValidInput(), CancellationToken.None);

        captured!.Status.Should().Be(OrderStatus.Placed);
        captured.CustomerId.Should().Be("cust-1");
        captured.Currency.Should().Be("CAD");
        captured.Total.Amount.Should().Be(20m);
        captured.ShippingAddress.Should().Be(Address.Create("1 King St", "Toronto", "M5H 1A1", "CA"));
        captured.Lines.Should().ContainSingle(l => l.Sku == "SKU-1" && l.Quantity == 2);
    }

    [Fact]
    public async Task PlaceOrder_WithNoLines_ThrowsValidation_AndNeverCommits()
    {
        var input = ValidInput() with { Lines = [] };

        var act = async () => await Sut().PlaceOrderAsync(input, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey(nameof(PlaceOrderInput.Lines));

        _repo.DidNotReceive().Add(Arg.Any<Order>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_WithABadCurrency_ThrowsValidation_BeforeTheDomainIsTouched()
    {
        var input = ValidInput() with { Currency = "CANADIAN" };

        var act = async () => await Sut().PlaceOrderAsync(input, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey(nameof(PlaceOrderInput.Currency));

        _repo.DidNotReceive().Add(Arg.Any<Order>());
    }

    [Fact]
    public async Task CancelOrder_CancelsTheAggregateAndCommits()
    {
        var order = PlacedOrder(_time.GetUtcNow().UtcDateTime);
        _repo.GetByIdAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);

        await Sut().CancelOrderAsync(order.Id.Value, "customer request", CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Cancelled);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelOrder_UnknownId_ThrowsNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var act = async () => await Sut().CancelOrderAsync(Guid.NewGuid(), "why", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrderById_UnknownId_ThrowsNotFound()
    {
        _readStore.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OrderDto?)null);

        var act = async () => await Sut().GetOrderByIdAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetOrderById_ReadsThroughTheReadStore_NotTheRepository()
    {
        var id = Guid.NewGuid();
        var dto = new OrderDto(id, "cust-1", "Placed", 20m, "CAD", DateTime.UtcNow, []);
        _readStore.GetAsync(id, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await Sut().GetOrderByIdAsync(id, CancellationToken.None);

        result.Should().BeSameAs(dto);
        await _repo.DidNotReceive().GetByIdAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1)]        // page floors to 1
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public async Task ListOrders_ClampsPage(int requested, int expected)
    {
        await Sut().ListOrdersAsync(requested, 20, null, CancellationToken.None);

        await _readStore.Received(1).ListAsync(expected, 20, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrders_ClampsPageSizeTo100()
    {
        await Sut().ListOrdersAsync(1, 10_000, null, CancellationToken.None);

        await _readStore.Received(1).ListAsync(1, 100, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOrders_PassesTheStatusFilterThrough()
    {
        var expected = new PagedResult<OrderDto>([], 1, 20, 0);
        _readStore.ListAsync(1, 20, "Placed", Arg.Any<CancellationToken>()).Returns(expected);

        var result = await Sut().ListOrdersAsync(1, 20, "Placed", CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}
