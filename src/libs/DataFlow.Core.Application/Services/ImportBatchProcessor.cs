using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Application.Options;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using ClosedXML.Excel;
using System.Linq;
using System.Collections.Generic;

namespace DataFlow.Core.Application.Services;

public class ImportBatchProcessor : IImportBatchProcessor
{
    private readonly ILogger<ImportBatchProcessor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IImportBatchRepository _batchRepository;
    private readonly IImportItemRepository _itemRepository;
    private readonly SensitiveDataOptions _sensitiveDataOptions;

    public ImportBatchProcessor(
        ILogger<ImportBatchProcessor> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IImportBatchRepository batchRepository,
        IImportItemRepository itemRepository,
        IOptions<SensitiveDataOptions>? sensitiveDataOptions = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _sensitiveDataOptions = sensitiveDataOptions?.Value ?? new SensitiveDataOptions();
    }

    public async Task ProcessBatchAsync(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var clientIdTag = new KeyValuePair<string, object?>("clientId", batch.ClientId.ToString());
        Metrics.BatchesProcessing.Record(1, clientIdTag);

        _logger.LogInformation(
            "Starting processing for batch {BatchId}, client {ClientId}, file {FileName}",
            batch.Id, batch.ClientId, batch.FileName);

        try
        {
            // 1. Validar arquivo existe
            if (!File.Exists(batch.UploadPath))
            {
                throw new FileNotFoundException($"Upload file not found: {batch.UploadPath}");
            }

            // 2. Parse do arquivo baseado no tipo
            var items = await ParseFileAsync(batch, cancellationToken);
            _logger.LogInformation("Parsed {Count} items from batch {BatchId}", items.Count, batch.Id);

            // 3. Persistir items iniciais
            await _itemRepository.AddRangeAsync(items, cancellationToken);
            
            // 4. Processar em lotes
            var batchSize = _configuration.GetValue<int>("ImportBatch:ChunkSize", 100);
            var omniFlowBaseUrl = _configuration.GetValue<string>("OmniFlow:BaseUrl") 
                ?? throw new InvalidOperationException("OmniFlow:BaseUrl not configured");

            var processedCount = 0;
            var errorCount = 0;
            var errorSummary = new List<string>();

            for (int i = 0; i < items.Count; i += batchSize)
            {
                var chunk = items.Skip(i).Take(batchSize).ToList();
                var chunkId = (i / batchSize) + 1;
                var offset = i;

                _logger.LogInformation("Sending chunk {ChunkId} (offset {Offset}, count {Count}) for batch {BatchId}",
                    chunkId, offset, chunk.Count, batch.Id);

                var (success, error) = await SendChunkToOmniFlowAsync(
                    batch.Id,
                    batch.ClientId,
                    chunkId,
                    offset,
                    chunk,
                    omniFlowBaseUrl,
                    cancellationToken);

                if (success)
                {
                    processedCount += chunk.Count;
                    var chunkTags = new TagList
                    {
                        { "clientId", batch.ClientId.ToString() },
                        { "batchId", batch.Id.ToString() }
                    };
                    Metrics.ChunkTotal.Add(1, chunkTags);
                }
                else
                {
                    errorCount += chunk.Count;
                    var errorTags = new TagList
                    {
                        { "clientId", batch.ClientId.ToString() },
                        { "batchId", batch.Id.ToString() }
                    };
                    Metrics.ChunkErrorsTotal.Add(1, errorTags);
                    if (!string.IsNullOrWhiteSpace(error))
                        errorSummary.Add($"Chunk {chunkId}: {error}");
                }
                
                ApplyPayloadRetentionPolicy(batch, chunk, success);
                // Atualizar items no banco
                await _itemRepository.UpdateRangeAsync(chunk, cancellationToken);
            }

            // 5. Atualizar batch
            batch.Complete(items.Count, processedCount, errorCount > 0 ? string.Join("; ", errorSummary) : null);
            await _batchRepository.UpdateAsync(batch, cancellationToken);
            
            stopwatch.Stop();
            var durationTags = new TagList
            {
                { "clientId", batch.ClientId.ToString() },
                { "status", batch.Status.ToString() }
            };
            Metrics.BatchDurationSeconds.Record(stopwatch.Elapsed.TotalSeconds, durationTags);
            Metrics.BatchTotal.Add(1, durationTags);
            Metrics.BatchesProcessing.Record(0, clientIdTag);

            _logger.LogInformation(
                "Completed processing batch {BatchId}, client {ClientId}: {Processed}/{Total} processed, {Errors} errors, duration {Duration}s",
                batch.Id, batch.ClientId, processedCount, items.Count, errorCount, stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Metrics.BatchesProcessing.Record(0, clientIdTag);
            var failedTags = new TagList
            {
                { "clientId", batch.ClientId.ToString() },
                { "status", "Failed" }
            };
            Metrics.BatchTotal.Add(1, failedTags);

            _logger.LogError(ex,
                "Failed to process batch {BatchId}, client {ClientId}: {Error}",
                batch.Id, batch.ClientId, ex.Message);
            batch.Fail(ex.Message);
            throw;
        }
    }

    private async Task<List<ImportItem>> ParseFileAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        return batch.FileType switch
        {
            ImportFileType.Json => await ParseJsonFileAsync(batch, cancellationToken),
            ImportFileType.Excel => await ParseExcelFileAsync(batch, cancellationToken),
            _ => throw new NotSupportedException($"File type {batch.FileType} is not supported")
        };
    }

    private async Task<List<ImportItem>> ParseJsonFileAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        var items = new List<ImportItem>();
        await using var stream = File.OpenRead(batch.UploadPath);
        
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var sequence = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var item = new ImportItem(
                    batch.Id,
                    sequence++,
                    element.GetRawText());
                items.Add(item);
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            // Se for objeto único, tratar como array de um elemento
            var item = new ImportItem(
                batch.Id,
                0,
                doc.RootElement.GetRawText());
            items.Add(item);
        }

        return items;
    }

    private async Task<List<ImportItem>> ParseExcelFileAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        var items = new List<ImportItem>();
        
        try
        {
            using var workbook = new XLWorkbook(batch.UploadPath);
            var worksheet = workbook.Worksheets.First();
            var rows = worksheet.RowsUsed().Skip(1); // Skip header
            
            var sequence = 0;
            foreach (var row in rows)
            {
                var rowData = new Dictionary<string, object?>();
                
                foreach (var cell in row.CellsUsed())
                {
                    var columnName = worksheet.Cell(1, cell.Address.ColumnNumber).GetString();
                    var value = cell.Value;
                    rowData[columnName] = value.ToString();
                }
                
                var json = JsonSerializer.Serialize(rowData);
                var item = new ImportItem(batch.Id, sequence++, json);
                items.Add(item);
            }
            
            _logger.LogInformation("Parsed {Count} rows from Excel file for batch {BatchId}", items.Count, batch.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Excel file for batch {BatchId}", batch.Id);
            throw;
        }
        
        return items;
    }

    private async Task<(bool Success, string? Error)> SendChunkToOmniFlowAsync(
        Guid batchId,
        Guid clientId,
        int chunkId,
        int offset,
        List<ImportItem> items,
        string omniFlowBaseUrl,
        CancellationToken cancellationToken)
    {
        var maxRetries = _configuration.GetValue<int>("ImportBatch:MaxRetries", 3);
        var retryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) };

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("OmniFlow");
                client.BaseAddress = new Uri(omniFlowBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);

                var payload = new
                {
                    batchId = batchId,
                    chunkId = chunkId,
                    offset = offset,
                    items = items.Select(i => JsonSerializer.Deserialize<JsonElement>(i.PayloadJson))
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Adicionar correlation ID (trace ID)
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/leads/import")
                {
                    Content = content
                };
                var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
                request.Headers.Add("x-trace-id", traceId);
                request.Headers.Add("x-batch-id", batchId.ToString());
                request.Headers.Add("x-chunk-id", chunkId.ToString());

                var response = await client.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Chunk {ChunkId} sent successfully to OmniFlow for batch {BatchId}, client {ClientId}, attempt {Attempt}",
                        chunkId, batchId, clientId, attempt + 1);

                    // Marcar items como Imported
                    foreach (var item in items)
                    {
                        item.MarkImported();
                    }

                    return (true, null);
                }

                // Retry em 5xx, não retry em 4xx (exceto 429)
                var shouldRetry = response.StatusCode >= System.Net.HttpStatusCode.InternalServerError ||
                                  response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

                if (!shouldRetry || attempt >= maxRetries)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "OmniFlow returned {StatusCode} for chunk {ChunkId}, batch {BatchId}, client {ClientId}: {Error}",
                        response.StatusCode, chunkId, batchId, clientId, errorBody);

                    foreach (var item in items)
                    {
                        item.MarkError($"HTTP {response.StatusCode}: {errorBody}");
                    }

                    return (false, $"HTTP {response.StatusCode}: {errorBody}");
                }

                // Backoff exponencial
                if (attempt < retryDelays.Length)
                {
                    var delay = retryDelays[attempt];
                    _logger.LogWarning(
                        "Retrying chunk {ChunkId} for batch {BatchId} after {Delay}s (attempt {Attempt}/{MaxRetries})",
                        chunkId, batchId, delay.TotalSeconds, attempt + 1, maxRetries + 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (attempt >= maxRetries)
                {
                    _logger.LogError(ex,
                        "Failed to send chunk {ChunkId} to OmniFlow for batch {BatchId}, client {ClientId} after {Attempt} attempts",
                        chunkId, batchId, clientId, attempt + 1);

                    foreach (var item in items)
                    {
                        item.MarkError(ex.Message);
                    }

                    return (false, ex.Message);
                }

                // Retry em exceções de rede/timeout
                if (attempt < retryDelays.Length)
                {
                    var delay = retryDelays[attempt];
                    _logger.LogWarning(ex,
                        "Retrying chunk {ChunkId} for batch {BatchId} after {Delay}s due to exception (attempt {Attempt}/{MaxRetries})",
                        chunkId, batchId, delay.TotalSeconds, attempt + 1, maxRetries + 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        return (false, "Max retries exceeded");
    }

    private void ApplyPayloadRetentionPolicy(ImportBatch batch, IEnumerable<ImportItem> items, bool chunkSuccess)
    {
        var policy = batch.Client?.Policies?.FirstOrDefault();
        var redactOnSuccess = policy?.RedactPayloadOnSuccess ?? _sensitiveDataOptions.RedactPayloadOnSuccess;
        var redactOnFailure = policy?.RedactPayloadOnFailure ?? _sensitiveDataOptions.RedactPayloadOnFailure;
        var includeHash = _sensitiveDataOptions.IncludePayloadHash;

        var shouldRedact = chunkSuccess ? redactOnSuccess : redactOnFailure;

        if (!shouldRedact)
            return;

        foreach (var item in items)
        {
            item.RedactPayload(includeHash);
        }
    }
}

