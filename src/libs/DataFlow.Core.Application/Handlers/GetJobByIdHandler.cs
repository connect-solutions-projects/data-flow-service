using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Application.Queries;
using DataFlow.Core.Domain.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataFlow.Core.Application.Handlers;

public class GetJobByIdHandler : IRequestHandler<GetJobByIdQuery, JobResponse?>
{
    private readonly IIngestionJobRepository _jobRepository;
    private readonly ILogger<GetJobByIdHandler> _logger;

    public GetJobByIdHandler(IIngestionJobRepository jobRepository, ILogger<GetJobByIdHandler> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<JobResponse?> Handle(GetJobByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting job by ID: {JobId}", request.JobId);

        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        
        if (job == null)
        {
            _logger.LogWarning("Job not found: {JobId}", request.JobId);
            return null;
        }

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
