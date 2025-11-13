using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;

namespace DataFlow.Infrastructure.Validation;

public class NoOpValidationRule : IValidationRule
{
    public string RuleName => "NoOp";
    public string RuleDescription => "Pass-through validation that always succeeds";
    public FileType SupportedFileType => FileType.Csv;
    public int Priority => 0;

    public Task<ValidationResult> ValidateAsync(Stream stream, string clientId, CancellationToken cancellationToken = default)
        => Task.FromResult(ValidationResult.Success(totalRecords: 0, validRecords: 0));

    public bool CanApplyTo(string clientId, FileType fileType) => true;
}
