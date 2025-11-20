using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataFlow.Infrastructure.Webhooks;

public class WebhookDeliveryService : IWebhookDeliveryService
{
    private readonly IngestionDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryService> _logger;
    private static readonly TimeSpan[] RetryDelays = new[]
    {
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    };

    public WebhookDeliveryService(
        IngestionDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task DeliverBatchFinalizedAsync(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _db.WebhookSubscriptions
            .AsNoTracking()
            .Where(w => w.ClientId == batch.ClientId && w.IsActive)
            .ToListAsync(cancellationToken);

        if (!subscriptions.Any())
        {
            _logger.LogDebug("No active webhook subscriptions for client {ClientId}, batch {BatchId}",
                batch.ClientId, batch.Id);
            return;
        }

        var payload = CreateWebhookPayload(batch);
        var tasks = subscriptions.Select(sub => DeliverWebhookAsync(sub, payload, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private object CreateWebhookPayload(ImportBatch batch)
    {
        var eventType = batch.Status switch
        {
            ImportBatchStatus.Completed => "BatchCompleted",
            ImportBatchStatus.CompletedWithErrors => "BatchCompletedWithErrors",
            ImportBatchStatus.Failed => "BatchFailed",
            _ => "BatchFinalized"
        };

        return new
        {
            @event = eventType,
            clientId = batch.ClientId.ToString(),
            batchId = batch.Id.ToString(),
            status = batch.Status.ToString(),
            metrics = new
            {
                totalRecords = batch.TotalRecords,
                processedRecords = batch.ProcessedRecords,
                errorCount = batch.TotalRecords - batch.ProcessedRecords,
                startedAt = batch.StartedAt,
                completedAt = batch.CompletedAt,
                durationSeconds = batch.CompletedAt.HasValue && batch.StartedAt.HasValue
                    ? (batch.CompletedAt.Value - batch.StartedAt.Value).TotalSeconds
                    : (double?)null
            },
            errorSummary = batch.ErrorSummary,
            timestamp = DateTime.UtcNow
        };
    }

    private async Task DeliverWebhookAsync(WebhookSubscription subscription, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeHmacSignature(json, timestamp, subscription.Secret);

        var client = _httpClientFactory.CreateClient("WebhookDelivery");
        client.Timeout = TimeSpan.FromSeconds(30);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
        {
            Content = content
        };
        request.Headers.Add("X-DataFlow-Signature", signature);
        request.Headers.Add("X-DataFlow-Timestamp", timestamp);
        request.Headers.Add("X-DataFlow-Event", "BatchFinalized");

        for (int attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            try
            {
                var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Webhook delivered successfully to {Url} for batch {BatchId}, attempt {Attempt}",
                        subscription.Url, JsonSerializer.Deserialize<JsonElement>(json).GetProperty("batchId").GetString(), attempt + 1);
                    return;
                }

                _logger.LogWarning(
                    "Webhook delivery failed to {Url}, status {StatusCode}, attempt {Attempt}",
                    subscription.Url, response.StatusCode, attempt + 1);

                if (attempt < RetryDelays.Length - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error delivering webhook to {Url}, attempt {Attempt}",
                    subscription.Url, attempt + 1);

                if (attempt < RetryDelays.Length - 1)
                {
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
            }
        }

        // Todas as tentativas falharam
        await RecordDeliveryFailureAsync(subscription.Id, "All retry attempts failed", cancellationToken);
    }

    private static string ComputeHmacSignature(string payload, string timestamp, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return string.Empty;

        var message = $"{timestamp}.{payload}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task RecordDeliveryFailureAsync(Guid webhookId, string error, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Recording webhook delivery failure for subscription {WebhookId}: {Error}",
            webhookId, error);
        // TODO: Implementar tabela WebhookDeliveryFailures se necessário
        await Task.CompletedTask;
    }
}

