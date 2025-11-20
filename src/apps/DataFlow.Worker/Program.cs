using DataFlow.Core.Application.DependencyInjection;
using DataFlow.Core.Application.Options;
using DataFlow.Core.Application.Services;
using DataFlow.Infrastructure.DependencyInjection;
using DataFlow.Worker.Consumers;
using DataFlow.Worker.Options;
using DataFlow.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using DataFlow.Observability;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;
using OpenTelemetry.Exporter.Prometheus;
using Microsoft.AspNetCore.Builder;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
        services.Configure<SensitiveDataOptions>(context.Configuration.GetSection(SensitiveDataOptions.SectionName));
        services.Configure<DataRetentionOptions>(context.Configuration.GetSection(DataRetentionOptions.SectionName));
        
        // Redis para cache e locks
        var redisConn = context.Configuration.GetValue<string>("ConnectionStrings:Redis") ?? "redis:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConn;
        });
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConn));
        
        // Registrar worker de processamento de batches
        services.AddHostedService<ImportBatchWorkerService>();
        services.AddHostedService<DataRetentionHostedService>();

        // OpenTelemetry (traces + metrics)
        services.AddDataFlowTelemetry("dataflow-worker", context.Configuration);
        
        // Adicionar Prometheus exporter separadamente
        services.AddOpenTelemetry()
            .WithMetrics(m =>
            {
                m.AddPrometheusExporter();
            });

        // Logging OpenTelemetry opcional removido para evitar conflitos de build

        services.AddMassTransit(x =>
        {
            x.AddConsumer<ProcessJobConsumer>();
            x.AddConsumer<DataFlow.Worker.Consumers.BatchReadyConsumer>();
            x.UsingRabbitMq((ctx, cfg) =>
            {
                var mqHost = context.Configuration.GetValue<string>("RabbitMq:Host")
                            ?? context.Configuration.GetValue<string>("RabbitMq__Host")
                            ?? context.Configuration["RABBIT:HOST"]
                            ?? context.Configuration["RABBIT__HOST"]
                            ?? "rabbitmq";
                var user = context.Configuration.GetValue<string>("RabbitMq:Username")
                            ?? context.Configuration.GetValue<string>("RabbitMq__Username")
                            ?? context.Configuration["RABBIT:USER"]
                            ?? context.Configuration["RABBIT__USER"]
                            ?? "guest";
                var pass = context.Configuration.GetValue<string>("RabbitMq:Password")
                            ?? context.Configuration.GetValue<string>("RabbitMq__Password")
                            ?? context.Configuration["RABBIT:PASSWORD"]
                            ?? context.Configuration["RABBIT__PASSWORD"]
                            ?? "guest";
                var vhost = context.Configuration.GetValue<string>("RabbitMq:VirtualHost")
                            ?? context.Configuration.GetValue<string>("RabbitMq__VirtualHost")
                            ?? context.Configuration["RABBIT:VHOST"]
                            ?? context.Configuration["RABBIT__VHOST"]
                            ?? "/";
                var portStr = context.Configuration.GetValue<string>("RabbitMq:Port")
                              ?? context.Configuration.GetValue<string>("RabbitMq__Port")
                              ?? context.Configuration["RABBIT:PORT"]
                              ?? context.Configuration["RABBIT__PORT"];

                if (ushort.TryParse(portStr, out var port))
                {
                    cfg.Host(mqHost, port, vhost, h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });
                }
                else
                {
                    cfg.Host(mqHost, vhost, h =>
                    {
                        h.Username(user);
                        h.Password(pass);
                    });
                }
                cfg.ReceiveEndpoint("process-job-queue", e =>
                {
                    e.ConfigureConsumer<ProcessJobConsumer>(ctx);
                });
                
                cfg.ReceiveEndpoint("batch-ready-queue", e =>
                {
                    e.ConfigureConsumer<DataFlow.Worker.Consumers.BatchReadyConsumer>(ctx);
                });
            });
        });
    })
    .UseDefaultServiceProvider(options =>
    {
        // Desabilitar validação de escopo no startup para permitir IServiceScopeFactory
        options.ValidateScopes = false;
        options.ValidateOnBuild = false;
    })
    .Build();

// Expor endpoint Prometheus para Worker usando ASP.NET Core minimal API
var metricsBuilder = WebApplication.CreateBuilder(new string[] { });
metricsBuilder.Services.AddOpenTelemetry()
    .WithMetrics(m =>
    {
        m.AddRuntimeInstrumentation();
        m.AddMeter("DataFlow");
        m.AddPrometheusExporter();
    });
var metricsApp = metricsBuilder.Build();
metricsApp.MapPrometheusScrapingEndpoint();
metricsApp.Urls.Add("http://0.0.0.0:9090");
_ = Task.Run(() => metricsApp.RunAsync());

await host.RunAsync();
