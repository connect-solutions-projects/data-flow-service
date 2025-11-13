using DataFlow.Core.Application.Commands;
using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataFlow.Core.Application.Handlers;

public class ResetJobHandler : IRequestHandler<ResetJobCommand, JobResponse>
{
    private readonly IIngestionJobRepository _jobRepository;
    private readonly ILogger<ResetJobHandler> _logger;

    public ResetJobHandler(IIngestionJobRepository jobRepository, ILogger<ResetJobHandler> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<JobResponse> Handle(ResetJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(request.JobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Job {request.JobId} not found");

        switch (job.Status)
        {
            case JobStatus.Failed:
                job.Retry();
                break;
            case JobStatus.Completed:
                job.ResetForReprocess();
                break;
            default:
                throw new InvalidOperationException($"Cannot reprocess job in status {job.Status}");
        }

        await _jobRepository.UpdateAsync(job, cancellationToken);

        _logger.LogInformation("Job {JobId} reset for reprocessing. Status: {Status}", job.Id, job.Status);

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
