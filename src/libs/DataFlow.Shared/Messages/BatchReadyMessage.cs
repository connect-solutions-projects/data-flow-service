namespace DataFlow.Shared.Messages;

public record BatchReadyMessage
{
    public Guid BatchId { get; init; }
    public Guid ClientId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public DateTime ReadyAt { get; init; }
}

