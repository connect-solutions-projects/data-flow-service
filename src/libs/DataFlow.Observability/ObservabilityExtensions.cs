using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace DataFlow.Observability
{
    public static class ObservabilityExtensions
    {
        public static IOpenTelemetryBuilder AddDataFlowTelemetry(
            this IServiceCollection services, string serviceName, IConfiguration cfg)
        {
            return services.AddOpenTelemetry()
              .ConfigureResource(r => r.AddService(serviceName: serviceName))
              .WithMetrics(m =>
              {
                  m.AddRuntimeInstrumentation();
                  m.AddMeter("DataFlow"); // mantém o Meter já usado:contentReference[oaicite:2]{index=2}
#if DEBUG
                  m.AddConsoleExporter();
#endif
                  m.AddOtlpExporter(o => {
                      var endpoint = cfg.GetValue<string>("Otel:Endpoint")
                            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                            ?? "http://otel-collector:4317";
                      o.Endpoint = new Uri(endpoint);
                      o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                  });
              })
              .WithTracing(t =>
              {
                  t.AddHttpClientInstrumentation();
                  t.AddSource("MassTransit");
#if DEBUG
                  t.AddConsoleExporter();
#endif
                  t.AddOtlpExporter(o => {
                      var endpoint = cfg.GetValue<string>("Otel:Endpoint")
                            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                            ?? "http://otel-collector:4317";
                      o.Endpoint = new Uri(endpoint);
                      o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                  });
              });
        }
    }
}
