using System.IO;
using System.Linq;
using DataFlow.Core.Domain.Entities;
using DataFlow.Infrastructure.Persistence;
using DataFlow.Observability;
using DataFlow.Worker.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataFlow.Worker.Services;

public class DataRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionHostedService> _logger;
    private readonly DataRetentionOptions _options;

    public DataRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<DataRetentionHostedService> logger,
        IOptions<DataRetentionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableCleanup)
        {
            _logger.LogInformation("Data retention cleanup disabled via configuration.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.CheckIntervalHours));
        _logger.LogInformation(
            "Data retention job enabled. Retaining {Days} days, running every {Hours} hour(s).",
            _options.BatchRetentionDays,
            _options.CheckIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldBatchesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Ignore cancellations
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing data retention cleanup.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
    }

    private async Task CleanupOldBatchesAsync(CancellationToken cancellationToken)
    {
        var maxPerRun = Math.Max(1, _options.MaxBatchesPerRun);
        var now = DateTime.UtcNow;
        var defaultRetention = Math.Max(1, _options.BatchRetentionDays);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();

        var candidates = await dbContext.ImportBatches
            .Include(b => b.Client)!.ThenInclude(c => c.Policies)
            .Where(b => b.CompletedAt != null)
            .OrderBy(b => b.CompletedAt)
            .Take(maxPerRun * 5)
            .ToListAsync(cancellationToken);

        Metrics.RetentionRunsTotal.Add(1);

        var eligible = new List<ImportBatch>();
        foreach (var batch in candidates)
        {
            var policy = batch.Client?.Policies?.FirstOrDefault();
            var retentionDays = policy?.RetentionDays ?? defaultRetention;
            var cutoff = now.AddDays(-Math.Max(1, retentionDays));
            if (batch.CompletedAt < cutoff)
            {
                eligible.Add(batch);
                if (eligible.Count >= maxPerRun)
                    break;
            }
        }

        if (!eligible.Any())
        {
            _logger.LogDebug("No batches eligible for cleanup at this run.");
            return;
        }

        dbContext.ImportBatches.RemoveRange(eligible);
        var rows = await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Removed {BatchCount} batches (rows affected: {Rows}) older than {Cutoff}.",
            eligible.Count,
            rows,
            now);
        Metrics.RetentionBatchesDeletedTotal.Add(eligible.Count);

        foreach (var batch in eligible)
        {
            DeleteBatchDirectory(batch.UploadPath);
        }
    }

    private void DeleteBatchDirectory(string? uploadPath)
    {
        if (string.IsNullOrWhiteSpace(uploadPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(uploadPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            Directory.Delete(directory, true);
            _logger.LogInformation("Deleted retention directory {Directory}.", directory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete retention directory for path {UploadPath}.", uploadPath);
        }
    }
}

