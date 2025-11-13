using DataFlow.Core.Application.DTOs;

namespace DataFlow.Core.Application.Services;

public interface IIngestionOrchestrator
{
    Task<JobResponse> StartIngestionAsync(CreateJobRequest request, Stream fileStream, CancellationToken cancellationToken = default);
    Task<ValidationResultDto> ProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<JobResponse> RetryFailedJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<ValidationResultDto> ReprocessJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<JobResponse?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default);
}
