using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataFlow.Infrastructure.Repositories;

public class SqlBatchLockRepository : IBatchLockRepository
{
    private readonly IngestionDbContext _db;
    private readonly ILogger<SqlBatchLockRepository> _logger;

    public SqlBatchLockRepository(IngestionDbContext db, ILogger<SqlBatchLockRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> TryAcquireLockAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        // Usar transação com UPDLOCK/HOLDLOCK para garantir exclusividade
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // SELECT com UPDLOCK e HOLDLOCK garante que apenas uma instância consegue ler e atualizar
            // Usar ExecuteSqlRaw para garantir o lock no SQL Server
            var lockEntity = await _db.Set<BatchLock>()
                .FromSqlRaw("SELECT Id, IsLocked, LockOwnerBatchId, LockedAt FROM BatchLocks WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1")
                .AsTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (lockEntity is null)
            {
                _logger.LogWarning("BatchLocks table not initialized. Creating default lock via SQL.");
                // Inserir via SQL direto já que BatchLock tem construtor privado
                await _db.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT 1 FROM BatchLocks WHERE Id = 1) INSERT INTO BatchLocks (Id, IsLocked, LockOwnerBatchId, LockedAt) VALUES (1, 0, NULL, NULL)",
                    cancellationToken);
                
                // Buscar novamente
                lockEntity = await _db.Set<BatchLock>()
                    .FromSqlRaw("SELECT Id, IsLocked, LockOwnerBatchId, LockedAt FROM BatchLocks WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1")
                    .AsTracking()
                    .FirstOrDefaultAsync(cancellationToken);
                
                if (lockEntity is null)
                {
                    _logger.LogError("Failed to create or retrieve BatchLock");
                    await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            if (lockEntity.IsLocked)
            {
                _logger.LogDebug("Batch lock already acquired by batch {BatchId}", lockEntity.LockOwnerBatchId);
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            // Usar método da entidade para adquirir lock
            try
            {
                lockEntity.Acquire(batchId);
                _db.Set<BatchLock>().Update(lockEntity);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to acquire lock - already locked");
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            _logger.LogInformation("Batch lock acquired for batch {BatchId}", batchId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring batch lock for batch {BatchId}", batchId);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    public async Task ReleaseLockAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockEntity = await _db.Set<BatchLock>()
                .FirstOrDefaultAsync(l => l.Id == 1, cancellationToken);

            if (lockEntity is not null && lockEntity.IsLocked)
            {
                var batchId = lockEntity.LockOwnerBatchId;
                lockEntity.Release();
                _db.Set<BatchLock>().Update(lockEntity);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Batch lock released for batch {BatchId}", batchId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing batch lock");
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    public async Task<bool> IsLockExpiredAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var lockEntity = await _db.Set<BatchLock>()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == 1, cancellationToken);

        if (lockEntity is null || !lockEntity.IsLocked || !lockEntity.LockedAt.HasValue)
            return false;

        var elapsed = DateTime.UtcNow - lockEntity.LockedAt.Value;
        return elapsed > timeout;
    }

    public async Task ForceReleaseExpiredLocksAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!await IsLockExpiredAsync(timeout, cancellationToken))
            return;

        _logger.LogWarning("Force releasing expired batch lock (timeout: {Timeout})", timeout);
        await ReleaseLockAsync(cancellationToken);
    }
}

