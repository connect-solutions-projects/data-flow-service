using DataFlow.Core.Domain.Entities;

namespace DataFlow.Core.Domain.Contracts;

public interface IBatchLockRepository
{
    /// <summary>
    /// Tenta adquirir o lock global usando UPDLOCK/HOLDLOCK.
    /// Retorna true se conseguiu adquirir, false se já está locked.
    /// </summary>
    Task<bool> TryAcquireLockAsync(Guid batchId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Libera o lock global.
    /// </summary>
    Task ReleaseLockAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verifica se o lock está ativo e se expirou (watchdog).
    /// </summary>
    Task<bool> IsLockExpiredAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Força a liberação de locks expirados.
    /// </summary>
    Task ForceReleaseExpiredLocksAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

