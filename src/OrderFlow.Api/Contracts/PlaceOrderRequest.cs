namespace OrderFlow.Api.Contracts;

using OrderFlow.Application.Orders.Dtos;

public sealed record PlaceOrderLineRequest(string Sku, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderRequest(
    string CustomerId,
    string Currency,
    string AddressLine1,
    string City,
    string PostalCode,
    string Country,
    IReadOnlyList<PlaceOrderLineRequest> Lines)
{
    public PlaceOrderInput ToInput() => new(
        CustomerId, Currency, AddressLine1, City, PostalCode, Country,
        Lines.Select(l => new PlaceOrderLineInput(l.Sku, l.Quantity, l.UnitPrice)).ToList());
}
