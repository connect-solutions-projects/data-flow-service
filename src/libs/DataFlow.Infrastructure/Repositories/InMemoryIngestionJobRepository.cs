using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;

namespace DataFlow.Infrastructure.Repositories;

public class InMemoryIngestionJobRepository : IIngestionJobRepository
{
    private readonly Dictionary<Guid, IngestionJob> _store = new();

    public Task<IngestionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var job) ? job : null);

    public Task<IReadOnlyList<IngestionJob>> GetByStatusAsync(JobStatus status, int limit = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IngestionJob>>(_store.Values.Where(j => j.Status == status).Take(limit).ToList());

    public Task<IReadOnlyList<IngestionJob>> GetByClientIdAsync(string clientId, int limit = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IngestionJob>>(_store.Values.Where(j => j.ClientId == clientId).Take(limit).ToList());

    public Task<IngestionJob> AddAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        _store[job.Id] = job;
        return Task.FromResult(job);
    }

    public Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        _store[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }

    public Task<int> GetPendingJobsCountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(j => j.Status == JobStatus.Pending));

    public Task<IReadOnlyList<IngestionJob>> GetJobsForRetryAsync(int maxRetryCount, int limit = 100, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<IngestionJob>>(
            _store.Values.Where(j => j.Status == JobStatus.Failed && j.RetryCount < maxRetryCount).Take(limit).ToList());
}
