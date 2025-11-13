using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.Contracts;

public interface IIngestionJobRepository
{
    Task<IngestionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngestionJob>> GetByStatusAsync(
        JobStatus status, 
        int limit = 100, 
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngestionJob>> GetByClientIdAsync(
        string clientId, 
        int limit = 100, 
        CancellationToken cancellationToken = default);
    Task<IngestionJob> AddAsync(IngestionJob job, CancellationToken cancellationToken = default);
    Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetPendingJobsCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngestionJob>> GetJobsForRetryAsync(
        int maxRetryCount, 
        int limit = 100, 
        CancellationToken cancellationToken = default);
}