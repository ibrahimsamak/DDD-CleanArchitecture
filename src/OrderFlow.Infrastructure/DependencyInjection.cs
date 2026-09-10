namespace OrderFlow.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Events;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("OrderDb"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReadStore, OrderReadStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
