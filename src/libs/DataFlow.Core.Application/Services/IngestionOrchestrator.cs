using DataFlow.Core.Application.Commands;
using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Application.Interfaces;
using DataFlow.Core.Application.Queries;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataFlow.Core.Application.Services;

public class IngestionOrchestrator : IIngestionOrchestrator
{
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;
    private readonly ILogger<IngestionOrchestrator> _logger;

    public IngestionOrchestrator(
        IMediator mediator,
        IFileStorageService fileStorage,
        INotificationService notificationService,
        ILogger<IngestionOrchestrator> logger)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<JobResponse> StartIngestionAsync(CreateJobRequest request, Stream fileStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting ingestion process for client {ClientId}, file: {FileName}", 
            request.ClientId, request.FileName);

        // 1. Criar job
        var createCommand = new CreateJobCommand
        {
            ClientId = request.ClientId,
            FileName = request.FileName,
            FileSize = request.FileSize,
            ContentType = request.ContentType,
            Checksum = request.Checksum,
            FileType = request.FileType
        };

        var job = await _mediator.Send(createCommand, cancellationToken);
        await _notificationService.NotifyJobCreatedAsync(job, cancellationToken);

        // 2. Salvar arquivo
        var filePath = await _fileStorage.UploadFileAsync(fileStream, request.FileName, request.ContentType, cancellationToken);
        _logger.LogInformation("File uploaded for job {JobId}: {FilePath}", job.Id, filePath);

        // 3. Processamento será disparado por mensageria (Worker)
        return job;
    }

    public async Task<ValidationResultDto> ProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _mediator.Send(new GetJobByIdQuery { JobId = jobId }, cancellationToken);
        if (job == null)
            throw new KeyNotFoundException($"Job {jobId} not found");

        await _notificationService.NotifyJobStartedAsync(job, cancellationToken);

        try
        {
            // Baixar arquivo
            using var fileStream = await _fileStorage.DownloadFileAsync(job.FileName, cancellationToken);
            
            // Processar arquivo
            var processCommand = new ProcessFileCommand
            {
                JobId = jobId,
                FileStream = fileStream,
                FileName = job.FileName,
                ContentType = job.ContentType ?? "application/octet-stream",
                FileSize = job.FileSize
            };

            var result = await _mediator.Send(processCommand, cancellationToken);
            
            // Notificar conclusão
            await _notificationService.NotifyJobCompletedAsync(job, result, cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", jobId);
            await _notificationService.NotifyJobFailedAsync(job, ex.Message, cancellationToken);
            throw;
        }
    }

    public async Task<JobResponse> RetryFailedJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var retryCommand = new RetryJobCommand { JobId = jobId };
        var job = await _mediator.Send(retryCommand, cancellationToken);
        
        await _notificationService.NotifyJobRetriedAsync(job, job.RetryCount, cancellationToken);
        
        // Reprocessar
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessJobAsync(job.Id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retry processing failed for job {JobId}", job.Id);
            }
        }, cancellationToken);

        return job;
    }

    public async Task<ValidationResultDto> ReprocessJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // Permite reprocessar jobs Failed (via Retry) e Completed (via Reset)
        var resetCommand = new ResetJobCommand { JobId = jobId };
        var job = await _mediator.Send(resetCommand, cancellationToken);

        await _notificationService.NotifyJobRetriedAsync(job, job.RetryCount, cancellationToken);

        // Processar imediatamente e retornar o resultado
        var result = await ProcessJobAsync(job.Id, cancellationToken);
        return result;
    }

    public async Task<JobResponse?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var query = new GetJobByIdQuery { JobId = jobId };
        return await _mediator.Send(query, cancellationToken);
    }
}
