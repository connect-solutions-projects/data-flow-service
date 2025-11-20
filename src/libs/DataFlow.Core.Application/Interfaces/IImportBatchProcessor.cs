using DataFlow.Core.Domain.Entities;

namespace DataFlow.Core.Application.Interfaces;

public interface IImportBatchProcessor
{
    /// <summary>
    /// Processa um batch de importação completo: parsing, lotes, handshake com OmniFlow.
    /// </summary>
    Task ProcessBatchAsync(ImportBatch batch, CancellationToken cancellationToken = default);
}

