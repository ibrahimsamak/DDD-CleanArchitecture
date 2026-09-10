namespace OrderFlow.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Domain.Orders;

public sealed class OrderReadStore(OrderDbContext db) : IOrderReadStore
{
    public Task<OrderDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Orders
            .AsNoTracking()
            .Where(o => o.Id == new OrderId(id))
            .Select(o => Project(o))
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<OrderDto>> ListAsync(
        int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsed))
        {
            query = query.Where(o => o.Status == parsed);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenBy(o => o.Id)                    // stable tiebreak: pagination without it can skip rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => Project(o))
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(items, page, pageSize, total);
    }

    // A single expression, reused by both queries, translated to SQL by EF.
    private static OrderDto Project(Order o) => new(
        o.Id.Value,
        o.CustomerId,
        o.Status.ToString(),
        o.Lines.Sum(l => l.UnitPrice.Amount * l.Quantity),
        o.Currency,
        o.CreatedAtUtc,
        o.Lines.Select(l => new OrderLineDto(
            l.Sku, l.Quantity, l.UnitPrice.Amount, l.UnitPrice.Amount * l.Quantity)).ToList());
}
