namespace OrderFlow.Application.Orders.Dtos;

public sealed record OrderLineDto(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);
