using DataFlow.Core.Application.DependencyInjection;
using DataFlow.Core.Application.Services;
using DataFlow.Infrastructure.DependencyInjection;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using DataFlow.Observability;
using DataFlow.Worker.Consumers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);

        // OpenTelemetry (traces + metrics)
        services.AddDataFlowTelemetry("dataflow-worker", context.Configuration);

        // Logging OpenTelemetry opcional removido para evitar conflitos de build

        services.AddMassTransit(x =>
        {
            x.AddConsumer<ProcessJobConsumer>();
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
            });
        });
    })
    .Build();

await host.RunAsync();
