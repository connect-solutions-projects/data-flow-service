using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IWebhookDeliveryService = DataFlow.Core.Domain.Contracts.IWebhookDeliveryService;

namespace DataFlow.Worker.Services;

public class ImportBatchWorkerService : BackgroundService
{
    private readonly ILogger<ImportBatchWorkerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _lockTimeout;

    public ImportBatchWorkerService(
        ILogger<ImportBatchWorkerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        
        var pollSeconds = configuration.GetValue<int>("ImportBatch:PollIntervalSeconds", 30);
        var lockMinutes = configuration.GetValue<int>("ImportBatch:LockTimeoutMinutes", 30);
        _pollInterval = TimeSpan.FromSeconds(pollSeconds);
        _lockTimeout = TimeSpan.FromMinutes(lockMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImportBatchWorkerService started");

        // Watchdog: liberar locks expirados periodicamente
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var lockRepository = scope.ServiceProvider.GetRequiredService<IBatchLockRepository>();
                    await lockRepository.ForceReleaseExpiredLocksAsync(_lockTimeout, stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in lock watchdog");
                }
            }
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBatchesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batches");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("ImportBatchWorkerService stopped");
    }

    private async Task ProcessPendingBatchesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var batchRepository = scope.ServiceProvider.GetRequiredService<IImportBatchRepository>();
        var lockRepository = scope.ServiceProvider.GetRequiredService<IBatchLockRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<IImportBatchProcessor>();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

        // Buscar batches pendentes ou agendados
        var pendingBatches = await batchRepository.GetPendingBatchesAsync(limit: 10, cancellationToken);
        var scheduledBatches = await batchRepository.GetScheduledBatchesAsync(limit: 10, cancellationToken);

        var batchesToProcess = pendingBatches.Concat(scheduledBatches)
            .OrderBy(b => b.CreatedAt)
            .ToList();

        if (batchesToProcess.Count == 0)
        {
            _logger.LogDebug("No pending batches to process");
            return;
        }

        _logger.LogInformation("Found {Count} batches to process", batchesToProcess.Count);

        foreach (var batch in batchesToProcess)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Tentar adquirir lock
            var lockAcquired = await lockRepository.TryAcquireLockAsync(batch.Id, cancellationToken);
            if (!lockAcquired)
            {
                _logger.LogDebug("Could not acquire lock for batch {BatchId}, skipping", batch.Id);
                continue;
            }

            try
            {
                _logger.LogInformation("Processing batch {BatchId}", batch.Id);

                // Marcar como Processing
                batch.MarkProcessing();
                await batchRepository.UpdateAsync(batch, cancellationToken);

                // Processar
                await processor.ProcessBatchAsync(batch, cancellationToken);

                // Atualizar batch após processamento
                await batchRepository.UpdateAsync(batch, cancellationToken);

                // Limpar arquivo temporário
                await CleanupBatchFileAsync(batch, cancellationToken);

                // Enviar webhook se batch finalizado
                if (batch.Status == ImportBatchStatus.Completed || 
                    batch.Status == ImportBatchStatus.CompletedWithErrors ||
                    batch.Status == ImportBatchStatus.Failed)
                {
                    await webhookService.DeliverBatchFinalizedAsync(batch, cancellationToken);
                }

                _logger.LogInformation("Successfully processed batch {BatchId}", batch.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch {BatchId}", batch.Id);
                batch.Fail(ex.Message);
                await batchRepository.UpdateAsync(batch, cancellationToken);
            }
            finally
            {
                // Sempre liberar lock
                await lockRepository.ReleaseLockAsync(cancellationToken);
            }
        }
    }

    private async Task CleanupBatchFileAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(batch.UploadPath))
            {
                File.Delete(batch.UploadPath);
                _logger.LogInformation("Deleted temporary file for batch {BatchId}: {Path}", batch.Id, batch.UploadPath);
            }

            // Limpar diretório do batch se estiver vazio
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

