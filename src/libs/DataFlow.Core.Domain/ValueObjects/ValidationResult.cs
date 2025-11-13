namespace DataFlow.Core.Domain.ValueObjects;

public record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; }
    public int TotalRecords { get; init; }
    public int ValidRecords { get; init; }
    public DateTime ValidationCompletedAt { get; init; }

    public ValidationResult(bool isValid, IReadOnlyList<ValidationError> errors, int totalRecords, int validRecords)
    {
        IsValid = isValid;
        Errors = errors ?? Array.Empty<ValidationError>();
        TotalRecords = totalRecords;
        ValidRecords = validRecords;
        ValidationCompletedAt = DateTime.UtcNow;
    }

    public static ValidationResult Success(int totalRecords, int validRecords) => 
        new(true, Array.Empty<ValidationError>(), totalRecords, validRecords);

    public static ValidationResult Failure(IReadOnlyList<ValidationError> errors, int totalRecords, int validRecords) => 
        new(false, errors, totalRecords, validRecords);

    public bool HasCriticalErrors => Errors.Any(e => e.IsCritical);
    public int ErrorCount => Errors.Count;
    public double SuccessRate => TotalRecords > 0 ? (double)ValidRecords / TotalRecords : 0;
}