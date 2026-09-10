namespace OrderFlow.Api.IntegrationTests;

using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Api.Contracts;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Proves the <c>rowversion</c> mapping actually does something. Two DbContexts load the same order,
/// both change it, and the second save must lose — otherwise "I configured optimistic concurrency"
/// is a claim, not a fact.
/// </summary>
[Collection(nameof(OrderApiCollection))]
public sealed class OrderConcurrencyTests(OrderApiFactory factory)
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private static PlaceOrderRequest Sample() => new(
        "cust-concurrency", "CAD", "1 King St", "Toronto", "M5H 1A1", "CA",
        [new PlaceOrderLineRequest("SKU-1", 2, 10m)]);

    private async Task<OrderId> PlaceAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/orders", Sample());
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        return new OrderId(created!.Id);
    }

    /// <summary>Each scope gets its own DbContext — two independent copies of the same row.</summary>
    private (IServiceScope Scope, OrderDbContext Db) NewContext()
    {
        var scope = factory.Services.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<OrderDbContext>());
    }

    [Fact]
    public async Task ConcurrentUpdates_TheSecondSave_ThrowsDbUpdateConcurrencyException()
    {
        var id = await PlaceAsync();

        var (scopeA, dbA) = NewContext();
        var (scopeB, dbB) = NewContext();

        using (scopeA)
        using (scopeB)
        {
            var fromA = await dbA.Orders.SingleAsync(o => o.Id == id);
            var fromB = await dbB.Orders.SingleAsync(o => o.Id == id);

            fromA.Cancel("A got there first", Now);
            fromB.Cancel("B was too slow", Now);

            await dbA.SaveChangesAsync();

            // B's UPDATE carries the rowversion it read, which SQL Server has since bumped,
            // so it matches no rows and EF reports the conflict instead of silently overwriting.
            var act = async () => await dbB.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
    }

    [Fact]
    public async Task ConcurrentUpdates_TheWinningWriteSurvives()
    {
        var id = await PlaceAsync();

        var (scopeA, dbA) = NewContext();
        var (scopeB, dbB) = NewContext();

        using (scopeA)
        using (scopeB)
        {
            var fromA = await dbA.Orders.SingleAsync(o => o.Id == id);
            var fromB = await dbB.Orders.SingleAsync(o => o.Id == id);

            fromA.Cancel("A got there first", Now);
            await dbA.SaveChangesAsync();

            fromB.Cancel("B was too slow", Now);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        }

        // The loser changed nothing: the order is exactly as A left it.
        var (scope, db) = NewContext();
        using (scope)
        {
            var persisted = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id);
            persisted.Status.Should().Be(OrderStatus.Cancelled);
        }
    }

    [Fact]
    public async Task Rowversion_IsStampedAndBumpedOnEveryWrite()
    {
        var id = await PlaceAsync();

        byte[] afterInsert;
        var (scopeA, dbA) = NewContext();
        using (scopeA)
        {
            var order = await dbA.Orders.SingleAsync(o => o.Id == id);
            afterInsert = order.Version;
            afterInsert.Should().NotBeEmpty("SQL Server stamps the rowversion on insert");

            order.Cancel("bump it", Now);
            await dbA.SaveChangesAsync();

            order.Version.Should().NotEqual(afterInsert, "the rowversion moves on every update");
        }
    }

    private sealed record CreatedResponse(Guid Id);
}
