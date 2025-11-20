using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using System.IO;
using DataFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataFlow.Infrastructure.Services;

public class BatchPurgeService : IBatchPurgeService
{
    private readonly IngestionDbContext _dbContext;
    private readonly ILogger<BatchPurgeService> _logger;

    public BatchPurgeService(IngestionDbContext dbContext, ILogger<BatchPurgeService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PurgeResult> PurgeOlderThanAsync(int days, int maxBatches = 500, CancellationToken cancellationToken = default)
    {
        if (days < 0)
            throw new ArgumentOutOfRangeException(nameof(days));
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var batches = await _dbContext.ImportBatches
            .Where(b => b.CompletedAt != null && b.CompletedAt < cutoff)
            .OrderBy(b => b.CompletedAt)
            .Take(maxBatches)
            .Select(b => new { Entity = b, b.UploadPath })
            .ToListAsync(cancellationToken);

        return await PurgeInternalAsync(batches.Select(b => b.Entity).ToList(), cancellationToken);
    }

    public async Task<PurgeResult> PurgeByIdsAsync(IEnumerable<Guid> batchIds, CancellationToken cancellationToken = default)
    {
        var ids = batchIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
            return new PurgeResult();

        var batches = await _dbContext.ImportBatches
            .Where(b => ids.Contains(b.Id))
            .ToListAsync(cancellationToken);

        return await PurgeInternalAsync(batches, cancellationToken);
    }

    private async Task<PurgeResult> PurgeInternalAsync(List<ImportBatch> batches, CancellationToken cancellationToken)
    {
        if (batches.Count == 0)
        {
            _logger.LogInformation("No batches found for purge.");
            return new PurgeResult { BatchesRemoved = 0, FilesRemoved = 0 };
        }

        var filesRemoved = 0;
        foreach (var batch in batches)
        {
            filesRemoved += DeleteBatchDirectory(batch.UploadPath);
        }

        _dbContext.ImportBatches.RemoveRange(batches);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Purged {Count} batches via admin request.", batches.Count);
        return new PurgeResult
        {
            BatchesRemoved = batches.Count,
            FilesRemoved = filesRemoved
        };
    }

    private static int DeleteBatchDirectory(string? uploadPath)
    {
        if (string.IsNullOrWhiteSpace(uploadPath))
            return 0;

        try
        {
            var directory = Path.GetDirectoryName(uploadPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return 0;

            Directory.Delete(directory, true);
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}

