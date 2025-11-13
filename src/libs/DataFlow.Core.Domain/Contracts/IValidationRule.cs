using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;

namespace DataFlow.Core.Domain.Contracts;

public interface IValidationRule
{
    string RuleName { get; }
    string RuleDescription { get; }
    FileType SupportedFileType { get; }
    int Priority { get; }
    
    Task<ValidationResult> ValidateAsync(
        Stream data, 
        string clientId,
        CancellationToken cancellationToken = default);

    bool CanApplyTo(string clientId, FileType fileType);
}