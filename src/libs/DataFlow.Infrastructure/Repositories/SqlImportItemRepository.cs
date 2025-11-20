using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataFlow.Infrastructure.Repositories;

public class SqlImportItemRepository : IImportItemRepository
{
    private readonly IngestionDbContext _db;

    public SqlImportItemRepository(IngestionDbContext db)
    {
        _db = db;
    }

    public async Task AddRangeAsync(IEnumerable<ImportItem> items, CancellationToken cancellationToken = default)
    {
        await _db.ImportItems.AddRangeAsync(items, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<ImportItem> items, CancellationToken cancellationToken = default)
    {
        _db.ImportItems.UpdateRange(items);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImportItem>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await _db.ImportItems
            .AsNoTracking()
            .Where(i => i.BatchId == batchId)
            .OrderBy(i => i.Sequence)
            .ToListAsync(cancellationToken);
}

