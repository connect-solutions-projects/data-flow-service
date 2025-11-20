using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.Contracts;

public interface IImportBatchRepository
{
    Task<ImportBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportBatch>> GetPendingBatchesAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportBatch>> GetScheduledBatchesAsync(int limit = 10, CancellationToken cancellationToken = default);
    Task<ImportBatch> AddAsync(ImportBatch batch, CancellationToken cancellationToken = default);
    Task UpdateAsync(ImportBatch batch, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
}

