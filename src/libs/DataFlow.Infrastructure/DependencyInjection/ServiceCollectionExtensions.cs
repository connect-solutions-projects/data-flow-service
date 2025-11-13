using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Infrastructure.Integrations;
using DataFlow.Infrastructure.Notifications;
using DataFlow.Infrastructure.Parsers;
using DataFlow.Infrastructure.Persistence;
using DataFlow.Infrastructure.Repositories;
using DataFlow.Infrastructure.Storage;
using DataFlow.Infrastructure.Validation;
using DataFlow.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http; // IHttpClientFactory
namespace DataFlow.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ----------------------------
        // Persistence
        // ----------------------------
        var connectionString = configuration.GetConnectionString("DataFlow");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fallback: construir a connection string a partir de variáveis POSTGRES__*
            var host = configuration["POSTGRES:HOST"] ?? configuration["POSTGRES__HOST"];
            var port = configuration["POSTGRES:PORT"] ?? configuration["POSTGRES__PORT"];
            var db = configuration["POSTGRES:DB"] ?? configuration["POSTGRES__DB"];
            var user = configuration["POSTGRES:USER"] ?? configuration["POSTGRES__USER"];
            var pass = configuration["POSTGRES:PASSWORD"] ?? configuration["POSTGRES__PASSWORD"];

            if (!string.IsNullOrWhiteSpace(host) &&
                !string.IsNullOrWhiteSpace(db) &&
                !string.IsNullOrWhiteSpace(user) &&
                !string.IsNullOrWhiteSpace(pass))
            {
                var p = string.IsNullOrWhiteSpace(port) ? "5432" : port;
                connectionString = $"Host={host};Port={p};Database={db};Username={user};Password={pass}";
            }
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<IngestionDbContext>(options => options.UseNpgsql(connectionString));
            services.AddScoped<IIngestionJobRepository, PostgresIngestionJobRepository>();
        }
        else
        {
            // Fallback para repositório em memória quando não há connection string
            services.AddSingleton<IIngestionJobRepository, InMemoryIngestionJobRepository>();
        }

        // ----------------------------
        // Serviços básicos
        // ----------------------------
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<INotificationService, NoOpNotificationService>();
        services.AddSingleton<IFileParser, CsvFileParser>();
        services.AddSingleton<IValidationRule, NoOpValidationRule>();

        // ----------------------------
        // Integrações (Prometheus / Grafana)
        // ----------------------------
        // HttpClient base para integrações (ajuste políticas aqui se quiser)
        services.AddHttpClient("integrations");

        // IPrometheusClient -> PrometheusClient
        services.AddSingleton<IPrometheusClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("integrations");
            // Preferência: appsettings.json -> env var -> valor padrão
            var baseUrl = configuration.GetValue<string>("Prometheus:BaseUrl")
                        ?? Environment.GetEnvironmentVariable("PROMETHEUS_URL")
                        ?? "http://prometheus:9090";
            return new PrometheusClient(http, baseUrl);
        });

        // IGrafanaLinkBuilder -> GrafanaClient
        services.AddSingleton<IGrafanaLinkBuilder>(sp =>
        {
            var baseUrl = configuration.GetValue<string>("Grafana:BaseUrl")
                        ?? Environment.GetEnvironmentVariable("GRAFANA_URL")
                        ?? "http://grafana:3000";
            return new GrafanaClient(baseUrl);
        });

        return services;
    }
}
