using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Application.DTOs;

public record ValidationResultDto
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<ValidationErrorDto> Errors { get; init; }
    public required int TotalRecords { get; init; }
    public required int ValidRecords { get; init; }
    public required double SuccessRate { get; init; }
    public required DateTime ValidationCompletedAt { get; init; }
}

public record ValidationErrorDto
{
    public required string Field { get; init; }
    public required string Message { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public int? LineNumber { get; init; }
}