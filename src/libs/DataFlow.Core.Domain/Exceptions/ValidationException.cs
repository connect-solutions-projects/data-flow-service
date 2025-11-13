using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;

namespace DataFlow.Core.Domain.Exceptions;

public class ValidationException : DomainException
{
    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base("VALIDATION_FAILED", 
              $"Validation failed with {errors.Count} errors")
    {
        ValidationErrors = errors;
    }

    public ValidationException(string field, string message)
        : base("VALIDATION_FAILED", message)
    {
        ValidationErrors = new[] { new ValidationError(field, message, Enums.ErrorSeverity.Error) };
    }
}