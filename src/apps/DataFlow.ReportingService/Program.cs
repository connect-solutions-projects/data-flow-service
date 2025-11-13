using System.Text.Json;
using DataFlow.ReportingService.Services;

// ---- dependências compartilhadas e integrações
using DataFlow.Shared.Contracts;             // IPrometheusClient, IGrafanaLinkBuilder
using DataFlow.Infrastructure.Integrations;  // PrometheusClient, GrafanaClient
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// HttpClient padrão para as integrações
builder.Services.AddHttpClient();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Datlo Reporting Service API",
        Version = "v1",
        Description = "Serviço responsável por gerar relatórios com base nas métricas do pipeline."
    });
});

// Registro das dependências concretas com as interfaces
builder.Services.AddSingleton<IPrometheusClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var env = Environment.GetEnvironmentVariable("PROMETHEUS_URL");
    var baseUrl = ResolveUrl(sp.GetRequiredService<IHttpClientFactory>(), env,
        new[] { "http://prometheus:9090", "http://localhost:9090" }, "/-/ready");
    return new PrometheusClient(http, baseUrl);
});

builder.Services.AddSingleton<IGrafanaLinkBuilder>(sp =>
{
    var env = Environment.GetEnvironmentVariable("GRAFANA_URL");
    var baseUrl = ResolveUrl(sp.GetRequiredService<IHttpClientFactory>(), env,
        new[] { "http://grafana:3000", "http://localhost:3000" }, "/api/health");
    return new GrafanaClient(baseUrl);
});

// Serviço responsável pela geração dos relatórios
builder.Services.AddSingleton<ReportGenerator>();

var app = builder.Build();

// Middleware para arquivos estáticos (CSS customizado do Swagger)
app.UseStaticFiles();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DataFlow Reporting Service API v1");
        options.RoutePrefix = "swagger";
        options.DefaultModelsExpandDepth(-1);
        options.InjectStylesheet("/swagger-ui/custom.css");
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "reporting" }));

// ----------- ENDPOINT /reports/final -----------
app.MapPost("/reports/final", async (
    IPrometheusClient prom,
    IGrafanaLinkBuilder grafana,
    ReportGenerator generator,
    HttpContext ctx) =>
{
    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;
    var job = root.TryGetProperty("job", out var jobEl)
        ? jobEl.GetString() ?? "dataflow-api"
        : "dataflow-api";
    var window = root.TryGetProperty("window", out var winEl)
        ? winEl.GetString() ?? "5m"
        : "5m";
    var outputDir = root.TryGetProperty("outputDir", out var outEl)
        ? outEl.GetString()
        : null;

    var p95 = await prom.GetHttpApiP95Async(job, window);
    var statusRates = await prom.GetHttpStatusRateAsync(job, window);
    var active = await prom.GetActiveRequestsAsync(job);
    var grafanaLinks = grafana.BuildSuggestedLinks(job);

    var path = await generator.GenerateFinalReportAsync(
        p95, statusRates, active, grafanaLinks, outputDir);

    return Results.Ok(new { message = "Report generated", path });
})
.WithName("GenerateFinalReport")
.WithOpenApi(op =>
{
    op.Summary = "Gera o relatório consolidado com métricas reais.";
    op.Description = "Consulta Prometheus, monta links de Grafana e produz o relatório final em disco.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Reports" } };
    op.RequestBody = new OpenApiRequestBody
    {
        Required = false,
        Content =
        {
            ["application/json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["job"] = new OpenApiSchema { Type = "string", Description = "Nome do job/serviço a ser consultado no Prometheus." },
                        ["window"] = new OpenApiSchema { Type = "string", Description = "Janela de tempo PromQL (ex.: 5m, 1h)." },
                        ["outputDir"] = new OpenApiSchema { Type = "string", Description = "Diretório opcional para salvar o relatório." }
                    }
                }
            }
        }
    };
    op.Responses["200"] = new OpenApiResponse { Description = "Relatório gerado com sucesso." };
    op.Responses["500"] = new OpenApiResponse { Description = "Falha ao gerar relatório." };
    return op;
});

// ----------- ENDPOINT /reports/sample -----------
app.MapGet("/reports/sample", async (
    IPrometheusClient prom,
    IGrafanaLinkBuilder grafana,
    ReportGenerator generator) =>
{
    var p95 = 0.250; // exemplo 250ms
    var statusRates = new Dictionary<string, double>
    {
        { "200", 12.5 }, { "201", 3.2 }, { "500", 0.1 }
    };
    var active = 1.0;
    var links = grafana.BuildSuggestedLinks("dataflow-api");

    var path = await generator.GenerateFinalReportAsync(
        p95, statusRates, active, links, null, isSample: true);

    return Results.Ok(new { message = "Sample report generated", path });
})
.WithName("GenerateSampleReport")
.WithOpenApi(op =>
{
    op.Summary = "Gera um relatório de exemplo com dados mockados.";
    op.Description = "Útil para validar a estrutura e pipeline do serviço sem depender de métricas reais.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Reports" } };
    op.Responses["200"] = new OpenApiResponse { Description = "Relatório de exemplo gerado." };
    return op;
});

app.Run();

// ----------- Função auxiliar -----------
static string ResolveUrl(IHttpClientFactory httpClientFactory, string? envValue, string[] candidates, string healthPath)
{
    if (!string.IsNullOrWhiteSpace(envValue))
        return envValue;

    foreach (var c in candidates)
    {
        try
        {
            using var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromMilliseconds(750);
            var resp = http.GetAsync(new Uri(new Uri(c), healthPath)).GetAwaiter().GetResult();
            if (resp.IsSuccessStatusCode)
                return c;
        }
        catch { }
    }

    return candidates.Last();
}
