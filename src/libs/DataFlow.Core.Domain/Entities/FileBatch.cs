using DataFlow.Core.Domain.ValueObjects;

namespace DataFlow.Core.Domain.Entities;

public class FileBatch
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public string FileName { get; private set; }
    public long FileSize { get; private set; }
    public string Checksum { get; private set; }
    public int TotalRecords { get; private set; }
    public int ProcessedRecords { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? ProcessingCompletedAt { get; private set; }

    // EF Core
    private FileBatch() { }

    public FileBatch(Guid jobId, string fileName, long fileSize, string checksum)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("Job ID cannot be empty", nameof(jobId));
        
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));
        
        if (fileSize <= 0)
            throw new ArgumentException("File size must be greater than zero", nameof(fileSize));
        
        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum cannot be empty", nameof(checksum));

        Id = Guid.NewGuid();
        JobId = jobId;
        FileName = fileName;
        FileSize = fileSize;
        Checksum = checksum;
        TotalRecords = 0;
        ProcessedRecords = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public void StartProcessing()
    {
        ProcessingStartedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(int recordsProcessed)
    {
        if (recordsProcessed < 0)
            throw new ArgumentException("Processed records cannot be negative", nameof(recordsProcessed));

        ProcessedRecords = recordsProcessed;
    }

    public void Complete(int totalRecords)
    {
        TotalRecords = totalRecords;
        ProcessedRecords = totalRecords;
        ProcessingCompletedAt = DateTime.UtcNow;
    }

    public void SetTotalRecords(int totalRecords)
    {
        if (totalRecords < 0)
            throw new ArgumentException("Total records cannot be negative", nameof(totalRecords));

        TotalRecords = totalRecords;
    }

    public double GetProgressPercentage()
    {
        if (TotalRecords == 0)
            return 0;

        return (double)ProcessedRecords / TotalRecords * 100;
    }

    public TimeSpan? GetProcessingTime()
    {
        if (!ProcessingStartedAt.HasValue)
            return null;

        var endTime = ProcessingCompletedAt ?? DateTime.UtcNow;
        return endTime - ProcessingStartedAt.Value;
    }

    public bool IsProcessed() => ProcessingCompletedAt.HasValue;
    public bool IsProcessing() => ProcessingStartedAt.HasValue && !ProcessingCompletedAt.HasValue;
}