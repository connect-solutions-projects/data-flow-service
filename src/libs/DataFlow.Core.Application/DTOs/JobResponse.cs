using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Application.DTOs;

public record JobResponse
{
    public required Guid Id { get; init; }
    public required string ClientId { get; init; }
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required string FileType { get; init; }
    public string? ContentType { get; init; }
    public required JobStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? TotalRecords { get; init; }
    public int? ValidRecords { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
}
