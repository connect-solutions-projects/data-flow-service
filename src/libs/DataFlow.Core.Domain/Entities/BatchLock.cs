namespace DataFlow.Core.Domain.Entities;

public class BatchLock
{
    public int Id { get; private set; } = 1;
    public bool IsLocked { get; private set; }
    public Guid? LockOwnerBatchId { get; private set; }
    public DateTime? LockedAt { get; private set; }

    private BatchLock() { }

    public static BatchLock CreateUnlocked()
        => new() { IsLocked = false };

    public void Acquire(Guid batchId)
    {
        if (IsLocked)
            throw new InvalidOperationException("Lock already acquired.");

        IsLocked = true;
        LockOwnerBatchId = batchId;
        LockedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        IsLocked = false;
        LockOwnerBatchId = null;
        LockedAt = null;
    }
}

