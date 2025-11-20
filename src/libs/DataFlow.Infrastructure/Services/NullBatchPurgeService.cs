using DataFlow.Core.Domain.Contracts;

namespace DataFlow.Infrastructure.Services;

public class NullBatchPurgeService : IBatchPurgeService
{
    public Task<PurgeResult> PurgeOlderThanAsync(int days, int maxBatches = 500, CancellationToken cancellationToken = default)
        => Task.FromResult(new PurgeResult());

    public Task<PurgeResult> PurgeByIdsAsync(IEnumerable<Guid> batchIds, CancellationToken cancellationToken = default)
        => Task.FromResult(new PurgeResult());
}

