namespace DataFlow.Shared.Messages;

public record BatchCreatedMessage
{
    public Guid BatchId { get; init; }
    public Guid ClientId { get; init; }
    public string ClientIdentifier { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string PolicyDecision { get; init; } = "Immediate";
    public DateTime CreatedAt { get; init; }
}

