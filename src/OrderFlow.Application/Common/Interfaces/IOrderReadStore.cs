namespace OrderFlow.Application.Common.Interfaces;

using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders.Dtos;


public interface IOrderReadStore
{
    Task<OrderDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<OrderDto>> ListAsync(int page, int pageSize, string? status, CancellationToken ct = default);
}
