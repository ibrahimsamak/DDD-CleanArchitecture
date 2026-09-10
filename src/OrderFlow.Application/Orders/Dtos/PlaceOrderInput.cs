namespace OrderFlow.Application.Orders.Dtos;

public sealed record PlaceOrderLineInput(string Sku, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderInput(
    string CustomerId,
    string Currency,
    string AddressLine1,
    string City,
    string PostalCode,
    string Country,
    IReadOnlyList<PlaceOrderLineInput> Lines);
