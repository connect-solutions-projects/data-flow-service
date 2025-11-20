using DataFlow.Core.Application.DependencyInjection;
using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Application.Options;
using DataFlow.Core.Application.Services;
using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using DataFlow.Api.Services;
using DataFlow.Api.Services.Interfaces;
using DataFlow.Api.Options;
using Microsoft.Extensions.Options;
using DataFlow.Core.Domain.Enums;
using DataFlow.Infrastructure.Persistence;
using DataFlow.Infrastructure.Persistence.Seed;
using DataFlow.Observability;
using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Exporter.Prometheus;
using DataFlow.Shared.Messages;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OpenApi;
using System.Buffers;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// DI registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<ImportStorageOptions>(builder.Configuration.GetSection(ImportStorageOptions.SectionName));
builder.Services.Configure<SensitiveDataOptions>(builder.Configuration.GetSection(SensitiveDataOptions.SectionName));
builder.Services.AddScoped<IClientCredentialValidator, ClientCredentialValidator>();

var adminApiKey = builder.Configuration.GetValue<string>("Admin:ApiKey");

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
        // Prometheus exporter para endpoint /metrics
        m.AddPrometheusExporter();
    })
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
    });

// Logging OpenTelemetry desativado para evitar conflitos de DI/pacote

var app = builder.Build();

// Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint();

// Aplicar migrations opcionalmente no startup (evita EnsureCreated)
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<IngestionDbContext>();
    var seeder = scope.ServiceProvider.GetService<ClientSeeder>();
    var applyMigrations = builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup", false)
                      || string.Equals(Environment.GetEnvironmentVariable("APPLY_MIGRATIONS_ON_STARTUP"), "true", StringComparison.OrdinalIgnoreCase);
    if (applyMigrations && dbContext is not null)
    {
        await dbContext.Database.MigrateAsync();
    }

    if (seeder is not null)
    {
        await seeder.SeedAsync();
    }
}
catch
{
    // Se SQL Server não estiver disponível, seguimos com o fallback em memória
}

// Middleware
app.UseStaticFiles(); // Para servir arquivos estáticos (CSS customizado do Swagger)

if (builder.Configuration.GetValue<bool>("EnableHttpsRedirection", true))
{
    app.UseHttpsRedirection();
}

// Rate limiting por cliente
app.UseMiddleware<DataFlow.Api.Middleware.ClientRateLimitMiddleware>();

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

app.MapPost("/imports", async (
    HttpRequest request,
    IClientCredentialValidator credentialValidator,
    IngestionDbContext dbContext,
    IOptions<ImportStorageOptions> storageOptions,
    DataFlow.Core.Application.Services.IPolicyEvaluator policyEvaluator,
    IPublishEndpoint publishEndpoint,
    CancellationToken cancellationToken) =>
{
    var client = await credentialValidator.ValidateAsync(request, cancellationToken);
    if (client is null)
        return Results.Unauthorized();

    if (!request.HasFormContentType)
        return Results.BadRequest("Envie multipart/form-data contendo o arquivo e metadados.");

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0)
        return Results.BadRequest("Arquivo obrigatório (campo 'file').");

    var fileNameInput = form["fileName"].ToString();
    var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileNameInput) ? file.FileName : fileNameInput);
    var contentType = string.IsNullOrWhiteSpace(form["contentType"]) ? file.ContentType : form["contentType"].ToString();
    var originDefault = string.IsNullOrWhiteSpace(form["originDefault"]) ? null : form["originDefault"].ToString();
    var requestedBy = string.IsNullOrWhiteSpace(form["requestedBy"]) ? null : form["requestedBy"].ToString();
    var metadata = string.IsNullOrWhiteSpace(form["metadata"]) ? null : form["metadata"].ToString();

    var basePath = storageOptions.Value?.BasePath;
    if (string.IsNullOrWhiteSpace(basePath))
    {
        basePath = Path.Combine(Path.GetTempPath(), "dataflow", "imports");
    }

    var batchId = Guid.NewGuid();
    var batchDirectory = EnsureBatchDirectory(basePath, batchId);
    var destinationPath = Path.Combine(batchDirectory, safeFileName);
    var (checksum, bytesWritten) = await SaveBatchFileAsync(file, destinationPath, cancellationToken);
    var fileType = ResolveImportFileType(safeFileName, contentType);

    // Carregar client com policies para avaliação
    await dbContext.Entry(client).Collection(c => c.Policies).LoadAsync(cancellationToken);
    
    var batch = new ImportBatch(
        client.Id,
        fileType,
        safeFileName,
        bytesWritten,
        checksum,
        destinationPath,
        "Immediate",
        originDefault,
        requestedBy,
        metadata,
        batchId);
    
    // Atribuir client para policy evaluator
    batch.GetType().GetProperty("Client")?.SetValue(batch, client);

    // Avaliar policies
    var policyDecision = await policyEvaluator.EvaluateAsync(batch, cancellationToken);
    batch.SetPolicyDecision(policyDecision.Decision);
    
    if (policyDecision.ShouldSchedule)
    {
        batch.GetType().GetProperty("Status")?.SetValue(batch, 
            DataFlow.Core.Domain.Enums.ImportBatchStatus.Scheduled);
    }

    await dbContext.ImportBatches.AddAsync(batch, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    // Publicar evento BatchCreated
    try
    {
        await publishEndpoint.Publish(new BatchCreatedMessage
        {
            BatchId = batch.Id,
            ClientId = client.Id,
            ClientIdentifier = client.ClientIdentifier,
            FileName = batch.FileName,
            FileSizeBytes = batch.FileSizeBytes,
            PolicyDecision = batch.PolicyDecision,
            CreatedAt = batch.CreatedAt
        }, cancellationToken);
    }
    catch (Exception ex)
    {
        // Log mas não falha a requisição
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to publish BatchCreated event for batch {BatchId}", batch.Id);
    }

    // Se não foi agendado, publicar BatchReady imediatamente
    if (!policyDecision.ShouldSchedule)
    {
        try
        {
            await publishEndpoint.Publish(new BatchReadyMessage
            {
                BatchId = batch.Id,
                ClientId = client.Id,
                FileName = batch.FileName,
                ReadyAt = DateTime.UtcNow
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Failed to publish BatchReady event for batch {BatchId}", batch.Id);
        }
    }

    var response = new
    {
        batchId = batch.Id,
        status = batch.Status.ToString(),
        policyDecision = batch.PolicyDecision,
        scheduledFor = policyDecision.ScheduledFor,
        location = $"/imports/{batch.Id}"
    };

    if (policyDecision.ShouldSchedule)
    {
        return Results.Accepted($"/imports/{batch.Id}", response);
    }

    return Results.Accepted($"/imports/{batch.Id}", response);
})
.WithName("CreateImportBatch")
.WithOpenApi(op =>
{
    op.Summary = "Cria um novo batch de importação.";
    op.Description = "Recebe um arquivo JSON/Excel, armazena temporariamente e registra o lote como Pending.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Imports" } };
    op.Responses["202"] = new OpenApiResponse { Description = "Batch aceito para processamento." };
    op.Responses["400"] = new OpenApiResponse { Description = "Requisição inválida." };
    op.Responses["401"] = new OpenApiResponse { Description = "Credenciais inválidas." };
    return op;
});

app.MapGet("/imports/{batchId:guid}", async (
    Guid batchId,
    HttpRequest request,
    IClientCredentialValidator credentialValidator,
    IngestionDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var client = await credentialValidator.ValidateAsync(request, cancellationToken);
    if (client is null)
        return Results.Unauthorized();

    var batch = await dbContext.ImportBatches.AsNoTracking()
        .FirstOrDefaultAsync(b => b.Id == batchId && b.ClientId == client.Id, cancellationToken);

    if (batch is null)
        return Results.NotFound();

    var response = new
    {
        batchId = batch.Id,
        status = batch.Status.ToString(),
        fileName = batch.FileName,
        fileType = batch.FileType.ToString(),
        createdAt = batch.CreatedAt,
        startedAt = batch.StartedAt,
        completedAt = batch.CompletedAt,
        totalRecords = batch.TotalRecords,
        processedRecords = batch.ProcessedRecords,
        policyDecision = batch.PolicyDecision,
        errorSummary = batch.ErrorSummary
    };

    return Results.Ok(response);
})
.WithName("GetImportBatchStatus")
.WithOpenApi(op =>
{
    op.Summary = "Consulta o status de um batch.";
    op.Description = "Retorna progresso atualizado para a aplicação cliente.";
    op.Tags = new List<OpenApiTag> { new() { Name = "Imports" } };
    op.Responses["200"] = new OpenApiResponse { Description = "Batch encontrado." };
    op.Responses["401"] = new OpenApiResponse { Description = "Credenciais inválidas." };
    op.Responses["404"] = new OpenApiResponse { Description = "Batch não localizado." };
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

app.MapPost("/admin/purge", async (
    HttpRequest request,
    AdminPurgeRequest payload,
    IBatchPurgeService purgeService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorizedAdmin(request, adminApiKey))
    {
        logger.LogWarning("Unauthorized purge attempt.");
        return Results.Unauthorized();
    }

    if ((payload.BatchIds == null || !payload.BatchIds.Any()) && payload.OlderThanDays is null)
    {
        return Results.BadRequest("Informe batchIds ou olderThanDays.");
    }

    PurgeResult result;
    if (payload.BatchIds != null && payload.BatchIds.Any())
    {
        result = await purgeService.PurgeByIdsAsync(payload.BatchIds, cancellationToken);
    }
    else
    {
        var days = Math.Max(1, payload.OlderThanDays ?? 30);
        var max = Math.Max(1, payload.MaxBatches ?? 500);
        result = await purgeService.PurgeOlderThanAsync(days, max, cancellationToken);
    }

    return Results.Ok(result);
})
.WithTags("Admin")
.WithName("AdminPurgeBatches")
.WithOpenApi(op =>
{
    op.Summary = "Executa purge administrativo de batches.";
    op.Description = "Protegido pelo header X-Admin-Key (configurado em Admin:ApiKey).";
    op.Parameters.Add(new OpenApiParameter
    {
        Name = "X-Admin-Key",
        In = ParameterLocation.Header,
        Required = true,
        Description = "Chave de API administrativa."
    });
    return op;
});

app.Run();

static string EnsureBatchDirectory(string basePath, Guid batchId)
{
    var directory = Path.Combine(basePath, batchId.ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
}

static ImportFileType ResolveImportFileType(string fileName, string? contentType)
{
    var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
    if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) || extension == ".json")
    {
        return ImportFileType.Json;
    }

    return ImportFileType.Excel;
}

static async Task<(string checksum, long bytesWritten)> SaveBatchFileAsync(IFormFile file, string destinationPath, CancellationToken cancellationToken)
{
    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
    await using var source = file.OpenReadStream();
    await using var destination = File.Create(destinationPath);
    using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var buffer = ArrayPool<byte>.Shared.Rent(81920);
    long total = 0;
    try
    {
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hasher.AppendData(buffer, 0, read);
            total += read;
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }

    var checksum = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    return (checksum, total);
}

static bool IsAuthorizedAdmin(HttpRequest request, string? expectedKey)
{
    if (string.IsNullOrWhiteSpace(expectedKey))
        return false;
    if (!request.Headers.TryGetValue("X-Admin-Key", out var header))
        return false;

    var provided = header.ToString();
    if (string.IsNullOrEmpty(provided))
        return false;

    var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided);
    var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedKey);
    if (providedBytes.Length != expectedBytes.Length)
        return false;

    return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}

internal record AdminPurgeRequest
{
    public List<Guid>? BatchIds { get; init; }
    public int? OlderThanDays { get; init; }
    public int? MaxBatches { get; init; }
}
