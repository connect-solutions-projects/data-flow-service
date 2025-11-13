using DataFlow.Core.Application.Commands;
using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataFlow.Core.Application.Handlers;

public class ProcessFileHandler : IRequestHandler<ProcessFileCommand, ValidationResultDto>
{
    private readonly IIngestionJobRepository _jobRepository;
    private readonly IEnumerable<IFileParser> _parsers;
    private readonly IEnumerable<IValidationRule> _validationRules;
    private readonly ILogger<ProcessFileHandler> _logger;

    public ProcessFileHandler(
        IIngestionJobRepository jobRepository,
        IEnumerable<IFileParser> parsers,
        IEnumerable<IValidationRule> validationRules,
        ILogger<ProcessFileHandler> logger)
    {
        _jobRepository = jobRepository;
        _parsers = parsers;
        _validationRules = validationRules;
        _logger = logger;
    }

    public async Task<ValidationResultDto> Handle(ProcessFileCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing file for job: {JobId}", request.JobId);

        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job {request.JobId} not found");

        job.Start();
        await _jobRepository.UpdateAsync(job, cancellationToken);

        try
        {
            // Encontrar parser apropriado
            var parser = _parsers.FirstOrDefault(p => p.CanParse(request.FileName, request.ContentType))
                ?? throw new InvalidOperationException($"No parser found for file: {request.FileName}");

            // Contar registros
            var totalRecords = await parser.CountRecordsAsync(request.FileStream, cancellationToken);

            // Validar arquivo usando regras aplicáveis (stream-based)
            var validationErrors = new List<ValidationError>();
            var applicableRules = _validationRules.Where(r => r.CanApplyTo(job.ClientId, job.FileType))
                                                  .OrderBy(r => r.Priority);

            foreach (var rule in applicableRules)
            {
                // Resetar posição do stream se possível antes de cada regra
                if (request.FileStream.CanSeek)
                    request.FileStream.Position = 0;

                var result = await rule.ValidateAsync(request.FileStream, job.ClientId, cancellationToken);
                if (!result.IsValid)
                {
                    validationErrors.AddRange(result.Errors);
                }
            }

            var validRecords = Math.Max(0, totalRecords - validationErrors.Count); // aproximação
            var validationResult = ValidationResult.Failure(
                validationErrors,
                totalRecords: totalRecords,
                validRecords: validRecords
            );

            job.Complete(validationResult);
            await _jobRepository.UpdateAsync(job, cancellationToken);

            _logger.LogInformation("File processing completed for job: {JobId}, Errors: {ErrorCount}", 
                request.JobId, validationResult.ErrorCount);

            return new ValidationResultDto
            {
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors.Select(e => new ValidationErrorDto
                {
                    Field = e.Field,
                    Message = e.Message,
                    Severity = e.Severity,
                    LineNumber = e.LineNumber
                }).ToList(),
                TotalRecords = validationResult.TotalRecords,
                ValidRecords = validationResult.ValidRecords,
                SuccessRate = validationResult.SuccessRate,
                ValidationCompletedAt = validationResult.ValidationCompletedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file for job: {JobId}", request.JobId);
            job.Fail(ex.Message);
            await _jobRepository.UpdateAsync(job, cancellationToken);
            throw;
        }
    }
}
