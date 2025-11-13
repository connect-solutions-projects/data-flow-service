using DataFlow.Core.Application.DependencyInjection;
using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Application.Services;
using DataFlow.Core.Application.Interfaces;
using DataFlow.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using DataFlow.Api.Services;
using DataFlow.Api.Services.Interfaces;
using DataFlow.Api.Options;
using Microsoft.Extensions.Options;
using DataFlow.Observability;
using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using DataFlow.Shared.Messages;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// DI registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Datlo DataFlow API",
        Version = "v1",
        Description = "API de ingestão assíncrona de dados para o pipeline DataFlow."
    });
    c.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
});

// Redis Cache & Lock
var redisConn = builder.Configuration.GetValue<string>("Redis:ConnectionString")
                ?? builder.Configuration.GetValue<string>("Redis__ConnectionString");

if (string.IsNullOrWhiteSpace(redisConn))
{
    var redisHost = builder.Configuration.GetValue<string>("Redis:Host")
                    ?? builder.Configuration.GetValue<string>("Redis__Host")
                    ?? builder.Configuration["REDIS:HOST"]
                    ?? builder.Configuration["REDIS__HOST"]
                    ?? "localhost";
    var redisPort = builder.Configuration.GetValue<string>("Redis:Port")
                    ?? builder.Configuration.GetValue<string>("Redis__Port")
                    ?? builder.Configuration["REDIS:PORT"]
                    ?? builder.Configuration["REDIS__PORT"]
                    ?? "6379";

    redisConn = $"{redisHost}:{redisPort}";
}

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConn;
});
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddSingleton<IRedisLockService, RedisLockService>();
builder.Services.AddSingleton<IRedisRateLimiter, RedisRateLimiter>();
builder.Services.AddSingleton<IChecksumDedupService, ChecksumDedupService>();
// RateLimit options
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptionKeys.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptionKeys.SectionName));

// MassTransit (RabbitMQ) para publicação de mensagens
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var mq = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        var host = builder.Configuration.GetValue<string>("RabbitMq:Host")
                   ?? builder.Configuration.GetValue<string>("RabbitMq__Host")
                   ?? builder.Configuration["RABBIT:HOST"]
                   ?? builder.Configuration["RABBIT__HOST"]
                   ?? mq.Host;
        var user = builder.Configuration.GetValue<string>("RabbitMq:Username")
                   ?? builder.Configuration.GetValue<string>("RabbitMq__Username")
                   ?? builder.Configuration["RABBIT:USER"]
                   ?? builder.Configuration["RABBIT__USER"]
                   ?? mq.Username;
        var pass = builder.Configuration.GetValue<string>("RabbitMq:Password")
                   ?? builder.Configuration.GetValue<string>("RabbitMq__Password")
                   ?? builder.Configuration["RABBIT:PASSWORD"]
                   ?? builder.Configuration["RABBIT__PASSWORD"]
                   ?? mq.Password;
        var vhost = builder.Configuration.GetValue<string>("RabbitMq:VirtualHost")
                    ?? builder.Configuration.GetValue<string>("RabbitMq__VirtualHost")
                    ?? builder.Configuration["RABBIT:VHOST"]
                    ?? builder.Configuration["RABBIT__VHOST"]
                    ?? "/";
        var portStr = builder.Configuration.GetValue<string>("RabbitMq:Port")
                      ?? builder.Configuration.GetValue<string>("RabbitMq__Port")
                      ?? builder.Configuration["RABBIT:PORT"]
                      ?? builder.Configuration["RABBIT__PORT"];

        if (ushort.TryParse(portStr, out var port))
        {
            cfg.Host(host, port, vhost, h =>
            {
                h.Username(user);
                h.Password(pass);
            });
        }
        else
        {
            cfg.Host(host, vhost, h =>
            {
                h.Username(user);
                h.Password(pass);
            });
        }
    });
});

// OpenTelemetry (métricas + traces) via biblioteca compartilhada
builder.Services
    .AddDataFlowTelemetry("dataflow-api", builder.Configuration)
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation();
    })
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
    });

// Logging OpenTelemetry desativado para evitar conflitos de DI/pacote

var app = builder.Build();

// Aplicar migrations opcionalmente no startup (evita EnsureCreated)
try
{
    using var scope = app.Services.CreateScope();
    var svc = scope.ServiceProvider.GetService<DataFlow.Infrastructure.Persistence.IngestionDbContext>();
    var applyMigrations = builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup", false)
                          || string.Equals(Environment.GetEnvironmentVariable("APPLY_MIGRATIONS_ON_STARTUP"), "true", StringComparison.OrdinalIgnoreCase);
    if (applyMigrations && svc is not null)
    {
        svc.Database.Migrate();
    }
}
catch
{
    // Se Postgres não estiver disponível, seguimos com o fallback em memória
}

// Middleware
app.UseStaticFiles(); // Para servir arquivos estáticos (CSS customizado do Swagger)

if (builder.Configuration.GetValue<bool>("EnableHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DataFlow API v1");
        options.RoutePrefix = "swagger";
        options.DefaultModelsExpandDepth(-1);
        options.InjectStylesheet("/swagger-ui/custom.css");
    });
}

// Prometheus scraping endpoint (não habilitado sem exporter)

// Minimal health check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("GetHealthStatus")
   .WithOpenApi(op =>
   {
       op.Summary = "Retorna o status de saúde da API.";
       op.Description = "Útil para sondagens de disponibilidade (readiness/liveness).";
       op.Responses["200"] = new OpenApiResponse { Description = "Serviço operacional." };
       return op;
   });

// Create ingestion job (multipart/form-data)
app.MapPost("/ingestion/jobs", async (HttpRequest req, IIngestionOrchestrator orchestrator, IRedisRateLimiter rateLimiter, IChecksumDedupService dedup, IOptionsMonitor<RateLimitOptions> rlOptions, IPublishEndpoint bus, CancellationToken ct) =>
{
    var form = await req.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    if (file is null)
        return Results.BadRequest("Arquivo obrigatório: use campo 'file' (multipart/form-data)");

    var clientId = form["clientId"].ToString();
    var fileType = form["fileType"].ToString();
    var checksum = form["checksum"].ToString();
    var fileName = string.IsNullOrWhiteSpace(form["fileName"]) ? file.FileName : form["fileName"].ToString();
    var contentType = string.IsNullOrWhiteSpace(form["contentType"]) ? file.ContentType : form["contentType"].ToString();
    var fileSize = file.Length;

    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(fileType) || string.IsNullOrWhiteSpace(checksum))
        return Results.BadRequest("Campos obrigatórios: clientId, fileType, checksum");

    // Rate limiting por clientId (configurável e com tiers)
    var opts = rlOptions.CurrentValue;
    var limit = opts.Tiers.TryGetValue(clientId, out var tierLimit) ? tierLimit : opts.RequestsPerMinute;
    var decision = await rateLimiter.AllowAsync($"client:{clientId}", limit, TimeSpan.FromMinutes(1));
    // Cabeçalhos de rate limit
    var headers = req.HttpContext.Response.Headers;
    headers["X-RateLimit-Limit"] = decision.Limit.ToString();
    headers["X-RateLimit-Remaining"] = decision.Remaining.ToString();
    if (!decision.IsAllowed && decision.RetryAfterSeconds.HasValue)
    {
        headers["Retry-After"] = decision.RetryAfterSeconds.Value.ToString();
    }
    if (!decision.IsAllowed)
    {
        Metrics.RateLimit429Counter.Add(1);
        return Results.StatusCode(429);
    }

    // Deduplicação por checksum
    var existingOrReserved = await dedup.GetExistingOrReserveAsync(checksum, TimeSpan.FromHours(24));
    if (existingOrReserved.HasValue)
    {
        if (existingOrReserved.Value == Guid.Empty)
            return Results.Conflict(new { message = "Checksum já reservado. Tente novamente em instantes." });
        else
        {
            Metrics.DeduplicationHitCounter.Add(1);
            return Results.Conflict(new { message = "Arquivo duplicado.", jobId = existingOrReserved.Value, location = $"/ingestion/jobs/{existingOrReserved.Value}" });
        }
    }

    var requestDto = new CreateJobRequest
    {
        ClientId = clientId,
        FileName = fileName,
        FileSize = fileSize,
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
        Checksum = checksum,
        FileType = fileType
    };

    await using var stream = file.OpenReadStream();
    try
    {
        var job = await orchestrator.StartIngestionAsync(requestDto, stream, ct);
        await dedup.AssociateAsync(checksum, job.Id, TimeSpan.FromHours(24));
        // Publica mensagem para processamento no worker
        await bus.Publish(new ProcessJobMessage
        {
            JobId = job.Id,
            Trigger = "enqueue"
        }, ct);
        return Results.Created($"/ingestion/jobs/{job.Id}", job);
    }
    catch
    {
        // Libera reserva em caso de falha
        await dedup.ReleaseReservationAsync(checksum);
        throw;
    }
})
.WithName("CreateIngestionJob")
.WithOpenApi(op =>
{
    op.Summary = "Enfileira um novo job de ingestão.";
    op.Description = "Recebe um arquivo multipart/form-data, aplica validações iniciais e publica o job na fila para processamento assíncrono.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Ingestion" } };
    op.RequestBody = new OpenApiRequestBody
    {
        Required = true,
        Content =
        {
            ["multipart/form-data"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, OpenApiSchema>
                    {
                        ["file"] = new OpenApiSchema { Type = "string", Format = "binary", Description = "Arquivo a ser ingerido (CSV, JSON ou Parquet)." },
                        ["clientId"] = new OpenApiSchema { Type = "string", Description = "Identificador do cliente originador." },
                        ["fileType"] = new OpenApiSchema { Type = "string", Description = "Tipo de arquivo/ingestão (influencia regras de validação)." },
                        ["checksum"] = new OpenApiSchema { Type = "string", Description = "Checksum para deduplicação (ex.: SHA-256)." },
                        ["fileName"] = new OpenApiSchema { Type = "string", Description = "Nome amigável do arquivo (opcional)." },
                        ["contentType"] = new OpenApiSchema { Type = "string", Description = "Content-Type explícito (opcional)." }
                    },
                    Required = new HashSet<string> { "file", "clientId", "fileType", "checksum" }
                }
            }
        }
    };
    op.Responses["201"] = new OpenApiResponse { Description = "Job criado e enfileirado com sucesso." };
    op.Responses["400"] = new OpenApiResponse { Description = "Requisição inválida (campos ausentes ou malformados)." };
    op.Responses["409"] = new OpenApiResponse { Description = "Arquivo duplicado ou checksum já reservado." };
    op.Responses["429"] = new OpenApiResponse { Description = "Limite de requisições excedido." };
    op.Responses["500"] = new OpenApiResponse { Description = "Erro interno durante a ingestão." };
    return op;
});

// Get job status
app.MapGet("/ingestion/jobs/{id:guid}", async (Guid id, IIngestionOrchestrator orchestrator, IDistributedCache cache, CancellationToken ct) =>
{
    var cacheKey = $"job:{id}";
    var cached = await cache.GetStringAsync(cacheKey, ct);
    if (cached is not null)
    {
        return Results.Content(cached, "application/json");
    }

    var job = await orchestrator.GetJobStatusAsync(id, ct);
    if (job is null) return Results.NotFound();

    var json = JsonSerializer.Serialize(job);
    await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
    }, ct);
    return Results.Ok(job);
})
.WithName("GetIngestionJob")
.WithOpenApi(op =>
{
    op.Summary = "Consulta o status de um job de ingestão.";
    op.Description = "Recupera o status atual do job, consultando cache e persistência.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Ingestion" } };
    op.Responses["200"] = new OpenApiResponse { Description = "Status encontrado." };
    op.Responses["404"] = new OpenApiResponse { Description = "Job não localizado." };
    return op;
});

// Process job now (re-run processing)
app.MapPost("/ingestion/jobs/{id:guid}/process", async (Guid id, IIngestionOrchestrator orchestrator, IDistributedCache cache, IPublishEndpoint bus, CancellationToken ct) =>
{
    await bus.Publish(new ProcessJobMessage { JobId = id, Trigger = "process" }, ct);
    await cache.RemoveAsync($"job:{id}", ct);
    return Results.Accepted($"/ingestion/jobs/{id}");
})
.WithName("ProcessIngestionJob")
.WithOpenApi(op =>
{
    op.Summary = "Solicita processamento imediato de um job.";
    op.Description = "Publica mensagem para o worker processar novamente o job e invalida o cache.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Ingestion" } };
    op.Responses["202"] = new OpenApiResponse { Description = "Processamento re-enfileirado." };
    return op;
});

// Retry failed job
app.MapPost("/ingestion/jobs/{id:guid}/retry", async (Guid id, IIngestionOrchestrator orchestrator, CancellationToken ct) =>
{
    var job = await orchestrator.RetryFailedJobAsync(id, ct);
    return Results.Ok(job);
})
.WithName("RetryIngestionJob")
.WithOpenApi(op =>
{
    op.Summary = "Refaz o processamento de um job com falha.";
    op.Description = "Invoca o orquestrador para repetir a execução e devolver o resultado atualizado.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Ingestion" } };
    op.Responses["200"] = new OpenApiResponse { Description = "Job reprocessado." };
    op.Responses["404"] = new OpenApiResponse { Description = "Job não encontrado." };
    return op;
});

// Reprocess job (reset completed or retry failed) and return validation result
app.MapPost("/ingestion/jobs/{id:guid}/reprocess", async (Guid id, IIngestionOrchestrator orchestrator, IDistributedCache cache, IPublishEndpoint bus, CancellationToken ct) =>
{
    // Para reprocessar, resetamos/completados via orquestrador e publicamos a mensagem
    await orchestrator.ReprocessJobAsync(id, ct);
    await bus.Publish(new ProcessJobMessage { JobId = id, Trigger = "reprocess" }, ct);
    await cache.RemoveAsync($"job:{id}", ct);
    return Results.Accepted($"/ingestion/jobs/{id}");
})
.WithName("ReprocessIngestionJob")
.WithOpenApi(op =>
{
    op.Summary = "Reset e reprocessa um job, retornando resultado de validação.";
    op.Description = "Executa o fluxo de reprocessamento completo e reenvia o job para a fila.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Ingestion" } };
    op.Responses["202"] = new OpenApiResponse { Description = "Reprocessamento agendado." };
    return op;
});

// List uploaded files (diagnostics)
app.MapGet("/storage/uploads", async (IFileStorageService storage, IDistributedCache cache, CancellationToken ct) =>
{
    const string cacheKey = "storage:uploads";
    var cached = await cache.GetStringAsync(cacheKey, ct);
    if (cached is not null)
    {
        return Results.Content(cached, "application/json");
    }

    var files = await storage.ListFilesAsync(ct);
    var json = JsonSerializer.Serialize(files);
    await cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
    }, ct);
    return Results.Ok(files);
})
.WithName("ListUploadedFiles")
.WithOpenApi(op =>
{
    op.Summary = "Lista arquivos armazenados no bucket/local de uploads.";
    op.Description = "Endpoint diagnóstico para inspecionar arquivos enviados.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Storage" } };
    op.Responses["200"] = new OpenApiResponse { Description = "Lista retornada com sucesso." };
    return op;
});

app.Run();
