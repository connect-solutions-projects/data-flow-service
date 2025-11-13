using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.ValueObjects;

public record ValidationError
{
    public string Field { get; init; }
    public string Message { get; init; }
    public ErrorSeverity Severity { get; init; }
    public int? LineNumber { get; init; }

    public ValidationError(string field, string message, ErrorSeverity severity, int? lineNumber = null)
    {
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Field name cannot be empty", nameof(field));
        
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message cannot be empty", nameof(message));

        Field = field;
        Message = message;
        Severity = severity;
        LineNumber = lineNumber;
    }

    public bool IsCritical => Severity == ErrorSeverity.Critical;
    public bool IsError => Severity == ErrorSeverity.Error;
    public bool IsWarning => Severity == ErrorSeverity.Warning;
}