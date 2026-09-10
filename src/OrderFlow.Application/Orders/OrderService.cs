using FluentValidation;
using OrderFlow.Application.Common.Exceptions;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders.Dtos;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Orders.ValueObjects;
using ValidationException = OrderFlow.Application.Common.Exceptions.ValidationException;

namespace OrderFlow.Application.Orders;

public sealed class OrderService(
    IOrderRepository orders,
    IOrderReadStore readStore,
    IUnitOfWork unitOfWork,
    IValidator<PlaceOrderInput> placeOrderValidator,
    TimeProvider time
    ) : IOrderService
{

    public async Task<Guid> PlaceOrderAsync(PlaceOrderInput input, CancellationToken ct = default)
    {
        await ValidateAsync(placeOrderValidator, input, ct);
        var nowUtc = time.GetUtcNow().UtcDateTime;

        var address = Address.Create(input.AddressLine1, input.City, input.PostalCode, input.Country);
        var order = Order.Create(input.CustomerId, address, input.Currency, nowUtc);

        foreach (var line in input.Lines)
        {
            order.AddLine(line.Sku, line.Quantity, line.UnitPrice);
        }


        order.Place(nowUtc);   // raises OrderPlacedDomainEvent

        orders.Add(order);
        
        await unitOfWork.SaveChangesAsync(ct);

        return order.Id.Value;

    }

    public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken ct = default)
    {
        var order = await orders.GetByIdAsync(new OrderId(orderId), ct)
                    ?? throw new NotFoundException(nameof(Order), orderId);

        order.Cancel(reason, time.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<OrderDto> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default) =>
       await readStore.GetAsync(orderId, ct)
       ?? throw new NotFoundException(nameof(Order), orderId);

    public Task<PagedResult<OrderDto>> ListOrdersAsync(
        int page, int pageSize, string? status, CancellationToken ct = default) =>
        readStore.ListAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100), status, ct);


    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

        throw new ValidationException(errors);
    }
}