using DataFlow.Core.Application.Commands;
using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataFlow.Core.Application.Handlers;

public class CreateJobHandler : IRequestHandler<CreateJobCommand, JobResponse>
{
    private readonly IIngestionJobRepository _jobRepository;
    private readonly ILogger<CreateJobHandler> _logger;

    public CreateJobHandler(IIngestionJobRepository jobRepository, ILogger<CreateJobHandler> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<JobResponse> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new ingestion job for client {ClientId}, file: {FileName}", 
            request.ClientId, request.FileName);

        // Validar tipo de arquivo
        if (!Enum.TryParse<FileType>(request.FileType, true, out var fileType))
        {
            throw new ArgumentException($"Invalid file type: {request.FileType}");
        }

        var fileMetadata = new FileMetadata(
            request.FileName,
            request.FileSize,
            request.ContentType,
            request.Checksum,
            DateTime.UtcNow
        );

        var job = new IngestionJob(
            request.ClientId,
            fileType,
            fileMetadata
        );

        await _jobRepository.AddAsync(job, cancellationToken);

        _logger.LogInformation("Ingestion job created successfully: {JobId}", job.Id);

        return new JobResponse
        {
            Id = job.Id,
            ClientId = job.ClientId,
            FileName = job.FileMetadata.Name,
            FileSize = job.FileMetadata.Size,
            FileType = job.FileType.ToString(),
            ContentType = job.FileMetadata.ContentType,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            TotalRecords = job.ValidationResult?.TotalRecords,
            ValidRecords = job.ValidationResult?.ValidRecords,
            ErrorMessage = job.ErrorMessage,
            RetryCount = job.RetryCount
        };
    }
}
