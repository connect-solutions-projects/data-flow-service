using DataFlow.Core.Application.DTOs;

namespace DataFlow.Core.Application.Interfaces;

public interface INotificationService
{
    Task NotifyJobCreatedAsync(JobResponse job, CancellationToken cancellationToken = default);
    Task NotifyJobStartedAsync(JobResponse job, CancellationToken cancellationToken = default);
    Task NotifyJobCompletedAsync(JobResponse job, ValidationResultDto validationResult, CancellationToken cancellationToken = default);
    Task NotifyJobFailedAsync(JobResponse job, string errorMessage, CancellationToken cancellationToken = default);
    Task NotifyJobRetriedAsync(JobResponse job, int retryCount, CancellationToken cancellationToken = default);
}