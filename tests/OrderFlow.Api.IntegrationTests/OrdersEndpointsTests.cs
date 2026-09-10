namespace OrderFlow.Api.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderFlow.Api.Contracts;
using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders.Dtos;
using Xunit;

[Collection(nameof(OrderApiCollection))]
public sealed class OrdersEndpointsTests(OrderApiFactory factory)
{
    private static PlaceOrderRequest Sample() => new(
        "cust-1", "CAD", "1 King St", "Toronto", "M5H 1A1", "CA",
        [new PlaceOrderLineRequest("SKU-1", 2, 10m)]);

    private static async Task<Guid> PlaceAsync(HttpClient client, PlaceOrderRequest? request = null)
    {
        var post = await client.PostAsJsonAsync("/api/orders", request ?? Sample());
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await post.Content.ReadFromJsonAsync<CreatedResponse>();
        return created!.Id;
    }

    [Fact]
    public async Task PlaceOrder_ThenGet_RoundTripsThroughSql()
    {
        var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/orders", Sample());
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        post.Headers.Location.Should().NotBeNull();

        var created = await post.Content.ReadFromJsonAsync<CreatedResponse>();
        var order = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{created!.Id}");

        // The real proof: owned types, converters and the projection all survive a DB round trip.
        order!.Total.Should().Be(20m);
        order.Currency.Should().Be("CAD");
        order.Status.Should().Be("Placed");
        order.CustomerId.Should().Be("cust-1");
        order.Lines.Should().ContainSingle(l => l.Sku == "SKU-1" && l.LineTotal == 20m);
    }

    [Fact]
    public async Task PlaceOrder_InvalidPayload_Returns400ProblemDetails()
    {
        var client = factory.CreateClient();
        var bad = Sample() with { Lines = [] };

        var res = await client.PostAsJsonAsync("/api/orders", bad);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Lines");
    }

    [Fact]
    public async Task PlaceOrder_WithDuplicateSku_Returns409WithTheDomainErrorCode()
    {
        var client = factory.CreateClient();
        var duplicated = Sample() with
        {
            Lines =
            [
                new PlaceOrderLineRequest("SKU-1", 1, 10m),
                new PlaceOrderLineRequest("SKU-1", 1, 10m)
            ]
        };

        var res = await client.PostAsJsonAsync("/api/orders", duplicated);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Order.DuplicateSku");
    }

    [Fact]
    public async Task GetOrder_UnknownId_Returns404()
    {
        var res = await factory.CreateClient().GetAsync($"/api/orders/{Guid.NewGuid()}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelOrder_IsIdempotent()
    {
        var client = factory.CreateClient();
        var id = await PlaceAsync(client);

        var first = await client.PostAsJsonAsync($"/api/orders/{id}/cancel", new CancelOrderRequest("a"));
        var second = await client.PostAsJsonAsync($"/api/orders/{id}/cancel", new CancelOrderRequest("b"));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var order = await client.GetFromJsonAsync<OrderDto>($"/api/orders/{id}");
        order!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelOrder_UnknownId_Returns404()
    {
        var res = await factory.CreateClient()
            .PostAsJsonAsync($"/api/orders/{Guid.NewGuid()}/cancel", new CancelOrderRequest("gone"));

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListOrders_ReturnsAPageAndFiltersByStatus()
    {
        var client = factory.CreateClient();
        var id = await PlaceAsync(client, Sample() with { CustomerId = "cust-list" });

        var page = await client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?page=1&pageSize=50");

        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(50);
        page.TotalCount.Should().BeGreaterThan(0);
        page.Items.Should().Contain(o => o.Id == id);

        await client.PostAsJsonAsync($"/api/orders/{id}/cancel", new CancelOrderRequest("filtering"));

        var placed = await client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?status=Placed&pageSize=50");
        placed!.Items.Should().NotContain(o => o.Id == id);

        var cancelled = await client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/orders?status=Cancelled&pageSize=50");
        cancelled!.Items.Should().Contain(o => o.Id == id);
    }

    private sealed record CreatedResponse(Guid Id);
}
