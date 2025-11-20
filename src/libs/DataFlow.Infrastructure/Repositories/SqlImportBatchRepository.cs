using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataFlow.Infrastructure.Repositories;

public class SqlImportBatchRepository : IImportBatchRepository
{
    private readonly IngestionDbContext _db;

    public SqlImportBatchRepository(IngestionDbContext db)
    {
        _db = db;
    }

    public async Task<ImportBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.ImportBatches
            .Include(b => b.Client)!.ThenInclude(c => c.Policies)
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ImportBatch>> GetPendingBatchesAsync(int limit = 10, CancellationToken cancellationToken = default)
        => await _db.ImportBatches
            .AsNoTracking()
            .Include(b => b.Client)!.ThenInclude(c => c.Policies)
            .Where(b => b.Status == ImportBatchStatus.Pending)
            .OrderBy(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImportBatch>> GetScheduledBatchesAsync(int limit = 10, CancellationToken cancellationToken = default)
        => await _db.ImportBatches
            .AsNoTracking()
            .Include(b => b.Client)!.ThenInclude(c => c.Policies)
            .Where(b => b.Status == ImportBatchStatus.Scheduled)
            .OrderBy(b => b.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<ImportBatch> AddAsync(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        await _db.ImportBatches.AddAsync(batch, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task UpdateAsync(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        var local = _db.ImportBatches.Local.FirstOrDefault(e => e.Id == batch.Id);
        if (local is not null)
        {
            _db.Entry(local).State = EntityState.Detached;
        }
        _db.ImportBatches.Update(batch);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        => await _db.ImportBatches.CountAsync(b => b.Status == ImportBatchStatus.Pending, cancellationToken);
}

