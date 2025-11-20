using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.Entities;

public class ImportBatch
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public ImportBatchStatus Status { get; private set; }
    public ImportFileType FileType { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string Checksum { get; private set; } = string.Empty;
    public string UploadPath { get; private set; } = string.Empty;
    public string PolicyDecision { get; private set; } = "Immediate";
    public string? OriginDefault { get; private set; }
    public string? RequestedBy { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int TotalRecords { get; private set; }
    public int ProcessedRecords { get; private set; }
    public string? ErrorSummary { get; private set; }

    public Client Client { get; private set; } = null!;
    public ICollection<ImportItem> Items { get; private set; } = new List<ImportItem>();

    private ImportBatch() { }

    public ImportBatch(
        Guid clientId,
        ImportFileType fileType,
        string fileName,
        long fileSizeBytes,
        string checksum,
        string uploadPath,
        string policyDecision,
        string? originDefault,
        string? requestedBy,
        string? metadataJson,
        Guid? id = null)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException("ClientId cannot be empty.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName cannot be empty.", nameof(fileName));
        if (fileSizeBytes <= 0)
            throw new ArgumentException("FileSizeBytes must be greater than zero.", nameof(fileSizeBytes));
        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum cannot be empty.", nameof(checksum));
        if (string.IsNullOrWhiteSpace(uploadPath))
            throw new ArgumentException("UploadPath cannot be empty.", nameof(uploadPath));
        if (string.IsNullOrWhiteSpace(policyDecision))
            throw new ArgumentException("PolicyDecision cannot be empty.", nameof(policyDecision));

        Id = id ?? Guid.NewGuid();
        ClientId = clientId;
        FileType = fileType;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        Checksum = checksum;
        UploadPath = uploadPath;
        PolicyDecision = policyDecision;
        OriginDefault = originDefault;
        RequestedBy = requestedBy;
        MetadataJson = metadataJson;
        Status = ImportBatchStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkProcessing()
    {
        Status = ImportBatchStatus.Processing;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(int totalRecords, int processedRecords, string? errorSummary = null)
    {
        TotalRecords = totalRecords;
        ProcessedRecords = processedRecords;
        ErrorSummary = errorSummary;
        CompletedAt = DateTime.UtcNow;
        Status = string.IsNullOrWhiteSpace(errorSummary) || processedRecords == totalRecords
            ? ImportBatchStatus.Completed
            : ImportBatchStatus.CompletedWithErrors;
    }

    public void Fail(string error)
    {
        Status = ImportBatchStatus.Failed;
        ErrorSummary = error;
        CompletedAt = DateTime.UtcNow;
    }

    public void SetPolicyDecision(string decision)
    {
        PolicyDecision = decision;
    }
}

