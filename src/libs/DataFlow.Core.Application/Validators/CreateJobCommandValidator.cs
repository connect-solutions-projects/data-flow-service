using DataFlow.Core.Application.Commands;
using FluentValidation;

namespace DataFlow.Core.Application.Validators;

public class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    private static readonly string[] AllowedFileTypes = { "csv", "parquet", "json" };
    private const long MaxFileSize = 10L * 1024 * 1024 * 1024; // 10GB

    public CreateJobCommandValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("ClientId is required");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("FileName is required")
            .MaximumLength(255).WithMessage("FileName must not exceed 255 characters")
            .Must(HaveValidExtension).WithMessage("File must have a valid extension");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("FileSize must be greater than 0")
            .LessThanOrEqualTo(MaxFileSize).WithMessage($"FileSize must not exceed {MaxFileSize / (1024 * 1024 * 1024)}GB");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("ContentType is required")
            .Must(BeValidContentType).WithMessage("Invalid content type for file type");

        RuleFor(x => x.Checksum)
            .NotEmpty().WithMessage("Checksum is required")
            .MinimumLength(32).WithMessage("Checksum must be at least 32 characters");

        RuleFor(x => x.FileType)
            .NotEmpty().WithMessage("FileType is required")
            .Must(BeValidFileType).WithMessage($"FileType must be one of: {string.Join(", ", AllowedFileTypes)}");
    }

    private bool HaveValidExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" or ".parquet" or ".json" => true,
            _ => false
        };
    }

    private bool BeValidContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "text/csv" or "application/vnd.apache.parquet" or "application/json" => true,
            _ => false
        };
    }

    private bool BeValidFileType(string fileType)
    {
        return AllowedFileTypes.Contains(fileType.ToLowerInvariant());
    }
}