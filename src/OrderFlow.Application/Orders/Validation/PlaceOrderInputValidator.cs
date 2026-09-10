namespace OrderFlow.Application.Orders.Validation;

using FluentValidation;
using OrderFlow.Application.Orders.Dtos;

public sealed class PlaceOrderInputValidator : AbstractValidator<PlaceOrderInput>
{
    public PlaceOrderInputValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}
