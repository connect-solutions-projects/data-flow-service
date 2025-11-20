namespace DataFlow.Core.Domain.Contracts;

public interface IBatchPurgeService
{
    Task<PurgeResult> PurgeOlderThanAsync(int days, int maxBatches = 500, CancellationToken cancellationToken = default);
    Task<PurgeResult> PurgeByIdsAsync(IEnumerable<Guid> batchIds, CancellationToken cancellationToken = default);
}

public record PurgeResult
{
    public int BatchesRemoved { get; init; }
    public int FilesRemoved { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}

