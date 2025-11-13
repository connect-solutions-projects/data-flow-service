namespace DataFlow.Shared.Messages;

public record ProcessJobMessage
{
    public Guid JobId { get; init; }
    public string? Trigger { get; init; } // "process" | "reprocess" | "enqueue"
}
