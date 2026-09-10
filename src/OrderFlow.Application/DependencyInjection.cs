namespace OrderFlow.Application;

using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Common.Events;
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.EventHandlers;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IOrderService, OrderService>();

        //services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IDomainEventHandler, OrderPlacedLogHandler>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
