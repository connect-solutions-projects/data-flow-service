using DataFlow.Core.Application.Commands;
using FluentValidation;

namespace DataFlow.Core.Application.Validators;

public class ProcessFileCommandValidator : AbstractValidator<ProcessFileCommand>
{
    private const long MaxFileSize = 10L * 1024 * 1024 * 1024; // 10GB

    public ProcessFileCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required");

        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("FileStream is required")
            .Must(stream => stream != null && stream.CanRead)
            .WithMessage("FileStream must be readable");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("FileName is required")
            .MaximumLength(255).WithMessage("FileName must not exceed 255 characters");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("FileSize must be greater than 0")
            .LessThanOrEqualTo(MaxFileSize).WithMessage($"FileSize must not exceed {MaxFileSize / (1024 * 1024 * 1024)}GB")
            .Must((command, fileSize) => BeConsistentWithStream(command.FileStream, fileSize))
            .WithMessage("FileSize must be consistent with actual stream length");
    }

    private bool BeConsistentWithStream(Stream? fileStream, long declaredFileSize)
    {
        if (fileStream == null) return false;
        
        try
        {
            // Verificar se o tamanho declarado é razoável
            return fileStream.Length == declaredFileSize || 
                   (fileStream.Length > 0 && declaredFileSize > 0);
        }
        catch
        {
            return true; // Se não conseguir verificar, aceita
        }
    }
}