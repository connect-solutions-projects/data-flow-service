using DataFlow.Core.Domain.Entities;

namespace DataFlow.Core.Domain.Contracts;

public interface IImportItemRepository
{
    Task AddRangeAsync(IEnumerable<ImportItem> items, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<ImportItem> items, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportItem>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
}

