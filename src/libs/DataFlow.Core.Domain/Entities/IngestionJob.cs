using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;

namespace DataFlow.Core.Domain.Entities;

public class IngestionJob
{
    public Guid Id { get; private set; }
    public string ClientId { get; private set; }
    public FileType FileType { get; private set; }
    public JobStatus Status { get; private set; }
    public FileMetadata FileMetadata { get; private set; }
    public ValidationResult? ValidationResult { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }

    // EF Core
    private IngestionJob() { }

    public IngestionJob(string clientId, FileType fileType, FileMetadata fileMetadata, int maxRetries = 3)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be empty", nameof(clientId));

        Id = Guid.NewGuid();
        ClientId = clientId;
        FileType = fileType;
        FileMetadata = fileMetadata ?? throw new ArgumentNullException(nameof(fileMetadata));
        Status = JobStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        MaxRetries = maxRetries;
        RetryCount = 0;
    }

    public void Start()
    {
        if (Status != JobStatus.Pending)
            throw new InvalidOperationException($"Cannot start job in status {Status}");

        Status = JobStatus.Processing;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(ValidationResult validationResult)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot complete job in status {Status}");

        Status = JobStatus.Completed;
        ValidationResult = validationResult;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        if (Status != JobStatus.Processing)
            throw new InvalidOperationException($"Cannot fail job in status {Status}");

        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public bool CanRetry()
    {
        return Status == JobStatus.Failed && RetryCount < MaxRetries;
    }

    public void Retry()
    {
        if (!CanRetry())
            throw new InvalidOperationException("Job cannot be retried");

        Status = JobStatus.Pending;
        ErrorMessage = null;
        RetryCount++;
        CompletedAt = null;
    }

    public void ResetForReprocess()
    {
        if (Status != JobStatus.Completed)
            throw new InvalidOperationException($"Cannot reset job in status {Status}");

        Status = JobStatus.Pending;
        ErrorMessage = null;
        CompletedAt = null;
        ValidationResult = null;
    }

    public TimeSpan? GetProcessingTime()
    {
        if (!StartedAt.HasValue)
            return null;

        var endTime = CompletedAt ?? DateTime.UtcNow;
        return endTime - StartedAt.Value;
    }

    public bool IsLargeFile() => FileMetadata.IsLargeFile();
}
