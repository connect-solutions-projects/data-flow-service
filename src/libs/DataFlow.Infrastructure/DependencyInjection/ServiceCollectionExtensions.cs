using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Infrastructure.Integrations;
using DataFlow.Infrastructure.Notifications;
using DataFlow.Infrastructure.Parsers;
using DataFlow.Infrastructure.Persistence;
using DataFlow.Infrastructure.Repositories;
using DataFlow.Infrastructure.Persistence.Seed;
using DataFlow.Infrastructure.Services;
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
            // Fallback: construir a connection string a partir de variáveis SQLSERVER__*
            var host = configuration["SQLSERVER:HOST"] ?? configuration["SQLSERVER__HOST"];
            var port = configuration["SQLSERVER:PORT"] ?? configuration["SQLSERVER__PORT"];
            var db = configuration["SQLSERVER:DATABASE"] ?? configuration["SQLSERVER__DATABASE"];
            var user = configuration["SQLSERVER:USER"] ?? configuration["SQLSERVER__USER"];
            var pass = configuration["SQLSERVER:PASSWORD"] ?? configuration["SQLSERVER__PASSWORD"];

            if (!string.IsNullOrWhiteSpace(host) &&
                !string.IsNullOrWhiteSpace(db) &&
                !string.IsNullOrWhiteSpace(user) &&
                !string.IsNullOrWhiteSpace(pass))
            {
                var resolvedPort = string.IsNullOrWhiteSpace(port) ? string.Empty : $",{port}";
                connectionString = $"Server={host}{resolvedPort};Database={db};User Id={user};Password={pass};TrustServerCertificate=True;Encrypt=False;";
            }
        }

        services.Configure<ClientSeedOptions>(configuration.GetSection("ClientSeed"));
        services.AddTransient<ClientSeeder>();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<IngestionDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IIngestionJobRepository, SqlIngestionJobRepository>();
            services.AddScoped<IImportBatchRepository, SqlImportBatchRepository>();
            services.AddScoped<IImportItemRepository, SqlImportItemRepository>();
            services.AddScoped<IBatchPurgeService, BatchPurgeService>();
            
            // Configurar estratégia de lock (Redis ou SQL Server)
            services.Configure<DataFlow.Infrastructure.Options.BatchLockOptions>(
                configuration.GetSection(DataFlow.Infrastructure.Options.BatchLockOptions.SectionName));
            var lockProvider = configuration.GetValue<string>("BatchLock:Provider", "SqlServer");
            
            if (lockProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
            {
                // Redis lock requer IConnectionMultiplexer (deve estar registrado na API/Worker)
                services.AddScoped<IBatchLockRepository, DataFlow.Infrastructure.Repositories.RedisBatchLockRepository>();
            }
            else
            {
                services.AddScoped<IBatchLockRepository, SqlBatchLockRepository>();
            }
            
            // Webhooks
            services.AddHttpClient("WebhookDelivery");
            services.AddScoped<DataFlow.Core.Domain.Contracts.IWebhookDeliveryService, 
                DataFlow.Infrastructure.Webhooks.WebhookDeliveryService>();
        }
        else
        {
            // Fallback para repositório em memória quando não há connection string
            services.AddSingleton<IIngestionJobRepository, InMemoryIngestionJobRepository>();
            services.AddSingleton<IBatchPurgeService, NullBatchPurgeService>();
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
        
        // HttpClient para OmniFlow
        services.AddHttpClient("OmniFlow", (sp, client) =>
        {
            var baseUrl = configuration.GetValue<string>("OmniFlow:BaseUrl");
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });

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
