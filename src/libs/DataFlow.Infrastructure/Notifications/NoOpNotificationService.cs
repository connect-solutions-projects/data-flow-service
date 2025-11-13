using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DataFlow.Infrastructure.Notifications;

public class NoOpNotificationService : INotificationService
{
    private readonly ILogger<NoOpNotificationService> _logger;
    public NoOpNotificationService(ILogger<NoOpNotificationService> logger) => _logger = logger;

    public Task NotifyJobCreatedAsync(JobResponse job, CancellationToken cancellationToken = default)
    { _logger.LogInformation("Job created: {JobId}", job.Id); return Task.CompletedTask; }

    public Task NotifyJobStartedAsync(JobResponse job, CancellationToken cancellationToken = default)
    { _logger.LogInformation("Job started: {JobId}", job.Id); return Task.CompletedTask; }

    public Task NotifyJobCompletedAsync(JobResponse job, ValidationResultDto validationResult, CancellationToken cancellationToken = default)
    { _logger.LogInformation("Job completed: {JobId}, Valid: {IsValid}", job.Id, validationResult.IsValid); return Task.CompletedTask; }

    public Task NotifyJobFailedAsync(JobResponse job, string errorMessage, CancellationToken cancellationToken = default)
    { _logger.LogError("Job failed: {JobId}, Error: {Error}", job.Id, errorMessage); return Task.CompletedTask; }

    public Task NotifyJobRetriedAsync(JobResponse job, int retryCount, CancellationToken cancellationToken = default)
    { _logger.LogWarning("Job retried: {JobId}, Count: {Count}", job.Id, retryCount); return Task.CompletedTask; }
}

