using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataFlow.Infrastructure.Repositories;

public class PostgresIngestionJobRepository : IIngestionJobRepository
{
    private readonly IngestionDbContext _db;

    public PostgresIngestionJobRepository(IngestionDbContext db)
    {
        _db = db;
    }

    public async Task<IngestionJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.IngestionJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<IReadOnlyList<IngestionJob>> GetByStatusAsync(JobStatus status, int limit = 100, CancellationToken cancellationToken = default)
        => await _db.IngestionJobs.AsNoTracking()
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IngestionJob>> GetByClientIdAsync(string clientId, int limit = 100, CancellationToken cancellationToken = default)
        => await _db.IngestionJobs.AsNoTracking()
            .Where(j => j.ClientId == clientId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IngestionJob> AddAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        await _db.IngestionJobs.AddAsync(job, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        var local = _db.IngestionJobs.Local.FirstOrDefault(e => e.Id == job.Id);
        if (local is not null)
        {
            _db.Entry(local).State = EntityState.Detached;
        }
        _db.IngestionJobs.Update(job);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.IngestionJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (entity != null)
        {
            _db.IngestionJobs.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetPendingJobsCountAsync(CancellationToken cancellationToken = default)
        => await _db.IngestionJobs.CountAsync(j => j.Status == JobStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<IngestionJob>> GetJobsForRetryAsync(int maxRetryCount, int limit = 100, CancellationToken cancellationToken = default)
        => await _db.IngestionJobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.Failed && j.RetryCount < maxRetryCount)
            .OrderBy(j => j.RetryCount)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
