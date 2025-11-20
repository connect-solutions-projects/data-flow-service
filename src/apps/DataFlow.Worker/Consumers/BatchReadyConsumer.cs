using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Enums;
using DataFlow.Shared.Messages;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataFlow.Worker.Consumers;

public class BatchReadyConsumer : IConsumer<BatchReadyMessage>
{
    private readonly ILogger<BatchReadyConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public BatchReadyConsumer(
        ILogger<BatchReadyConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task Consume(ConsumeContext<BatchReadyMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received BatchReady event for batch {BatchId}", message.BatchId);

        using var scope = _scopeFactory.CreateScope();
        var batchRepository = scope.ServiceProvider.GetRequiredService<IImportBatchRepository>();
        var lockRepository = scope.ServiceProvider.GetRequiredService<IBatchLockRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<IImportBatchProcessor>();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

        try
        {
            var batch = await batchRepository.GetByIdAsync(message.BatchId, context.CancellationToken);
            if (batch == null)
            {
                _logger.LogWarning("Batch {BatchId} not found", message.BatchId);
                return;
            }

            // Verificar se já está processando ou processado
            if (batch.Status != ImportBatchStatus.Pending && batch.Status != ImportBatchStatus.Scheduled)
            {
                _logger.LogDebug("Batch {BatchId} is not in Pending/Scheduled status (current: {Status})", 
                    message.BatchId, batch.Status);
                return;
            }

            // Tentar adquirir lock
            var lockAcquired = await lockRepository.TryAcquireLockAsync(batch.Id, context.CancellationToken);
            if (!lockAcquired)
            {
                _logger.LogDebug("Could not acquire lock for batch {BatchId}, will retry later", message.BatchId);
                return;
            }

            try
            {
                _logger.LogInformation("Processing batch {BatchId} from RabbitMQ event", batch.Id);

                // Marcar como Processing
                batch.MarkProcessing();
                await batchRepository.UpdateAsync(batch, context.CancellationToken);

                // Processar
                await processor.ProcessBatchAsync(batch, context.CancellationToken);

                // Atualizar batch após processamento
                await batchRepository.UpdateAsync(batch, context.CancellationToken);

                // Limpar arquivo temporário
                await CleanupBatchFileAsync(batch, context.CancellationToken);

                // Enviar webhook se batch finalizado
                if (batch.Status == ImportBatchStatus.Completed || 
                    batch.Status == ImportBatchStatus.CompletedWithErrors ||
                    batch.Status == ImportBatchStatus.Failed)
                {
                    await webhookService.DeliverBatchFinalizedAsync(batch, context.CancellationToken);
                }

                _logger.LogInformation("Successfully processed batch {BatchId} from RabbitMQ", batch.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch {BatchId} from RabbitMQ", batch.Id);
                batch.Fail(ex.Message);
                await batchRepository.UpdateAsync(batch, context.CancellationToken);
            }
            finally
            {
                await lockRepository.ReleaseLockAsync(context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming BatchReady event for batch {BatchId}", message.BatchId);
            throw;
        }
    }

    private async Task CleanupBatchFileAsync(DataFlow.Core.Domain.Entities.ImportBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(batch.UploadPath))
            {
                File.Delete(batch.UploadPath);
                _logger.LogInformation("Deleted temporary file for batch {BatchId}: {Path}", batch.Id, batch.UploadPath);
            }

            var batchDir = Path.GetDirectoryName(batch.UploadPath);
            if (!string.IsNullOrWhiteSpace(batchDir) && Directory.Exists(batchDir))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(batchDir).Any())
                    {
                        Directory.Delete(batchDir);
                        _logger.LogInformation("Deleted empty batch directory: {Path}", batchDir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete batch directory {Path}", batchDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up file for batch {BatchId}", batch.Id);
        }
        await Task.CompletedTask;
    }
}

