using DataFlow.Core.Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DataFlow.Core.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IngestionOrchestrator).Assembly));
        services.AddScoped<IIngestionOrchestrator, IngestionOrchestrator>();
        return services;
    }
}

