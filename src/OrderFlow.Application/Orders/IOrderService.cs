using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders.Dtos;

namespace OrderFlow.Application.Orders;

public interface IOrderService
{
    Task<Guid> PlaceOrderAsync(PlaceOrderInput input, CancellationToken ct = default);
    Task CancelOrderAsync(Guid orderId, string reason, CancellationToken ct = default);
    Task<OrderDto> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<PagedResult<OrderDto>> ListOrdersAsync(int page, int pageSize, string? status, CancellationToken ct = default);
}