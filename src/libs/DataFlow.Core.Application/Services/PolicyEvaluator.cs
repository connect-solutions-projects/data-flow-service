using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Core.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace DataFlow.Core.Application.Services;

public interface IPolicyEvaluator
{
    Task<PolicyDecision> EvaluateAsync(ImportBatch batch, CancellationToken cancellationToken = default);
}

public class PolicyDecision
{
    public bool ShouldSchedule { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Decision { get; set; } = "Immediate";
}

public class PolicyEvaluator : IPolicyEvaluator
{
    private readonly IImportBatchRepository _batchRepository;
    private readonly ILogger<PolicyEvaluator> _logger;

    public PolicyEvaluator(
        IImportBatchRepository batchRepository,
        ILogger<PolicyEvaluator> logger)
    {
        _batchRepository = batchRepository;
        _logger = logger;
    }

    public async Task<PolicyDecision> EvaluateAsync(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        var decision = new PolicyDecision { Decision = "Immediate" };

        // Buscar policy do cliente
        var client = batch.Client;
        var policy = client?.Policies.FirstOrDefault();

        if (policy == null)
        {
            _logger.LogDebug("No policy found for client {ClientId}, batch {BatchId}, allowing immediate processing",
                batch.ClientId, batch.Id);
            return decision;
        }

        var fileSizeMb = batch.FileSizeBytes / (1024.0 * 1024.0);
        var currentHour = DateTime.UtcNow.Hour;

        // Verificar tamanho do arquivo
        if (policy.MaxFileSizeMb.HasValue && fileSizeMb > policy.MaxFileSizeMb.Value)
        {
            decision.ShouldSchedule = true;
            decision.Reason = $"File size {fileSizeMb:F2}MB exceeds limit of {policy.MaxFileSizeMb}MB";
            decision.Decision = "Scheduled";
            decision.ScheduledFor = GetNextAllowedTime(policy);
            _logger.LogInformation(
                "Batch {BatchId} scheduled due to file size: {Reason}, scheduled for {ScheduledFor}",
                batch.Id, decision.Reason, decision.ScheduledFor);
            return decision;
        }

        // Verificar se é arquivo grande e requer agendamento
        if (policy.RequireSchedulingForLarge && 
            policy.LargeThresholdMb.HasValue && 
            fileSizeMb > policy.LargeThresholdMb.Value)
        {
            // Verificar horário permitido
            if (policy.AllowedStartHour.HasValue && policy.AllowedEndHour.HasValue)
            {
                if (!IsWithinAllowedHours(currentHour, policy.AllowedStartHour.Value, policy.AllowedEndHour.Value))
                {
                    decision.ShouldSchedule = true;
                    decision.Reason = $"Large file ({fileSizeMb:F2}MB) outside allowed hours ({policy.AllowedStartHour}-{policy.AllowedEndHour} UTC)";
                    decision.Decision = "Scheduled";
                    decision.ScheduledFor = GetNextAllowedTime(policy);
                    _logger.LogInformation(
                        "Batch {BatchId} scheduled due to time window: {Reason}, scheduled for {ScheduledFor}",
                        batch.Id, decision.Reason, decision.ScheduledFor);
                    return decision;
                }
            }
        }

        // Verificar horário permitido para qualquer arquivo
        if (policy.AllowedStartHour.HasValue && policy.AllowedEndHour.HasValue)
        {
            if (!IsWithinAllowedHours(currentHour, policy.AllowedStartHour.Value, policy.AllowedEndHour.Value))
            {
                decision.ShouldSchedule = true;
                decision.Reason = $"Current hour {currentHour} outside allowed window ({policy.AllowedStartHour}-{policy.AllowedEndHour} UTC)";
                decision.Decision = "Scheduled";
                decision.ScheduledFor = GetNextAllowedTime(policy);
                _logger.LogInformation(
                    "Batch {BatchId} scheduled due to time window: {Reason}, scheduled for {ScheduledFor}",
                    batch.Id, decision.Reason, decision.ScheduledFor);
                return decision;
            }
        }

        // Verificar limite diário de batches
        if (policy.MaxBatchPerDay.HasValue)
        {
            var todayBatches = await CountBatchesTodayAsync(batch.ClientId, cancellationToken);
            if (todayBatches >= policy.MaxBatchPerDay.Value)
            {
                decision.ShouldSchedule = true;
                decision.Reason = $"Daily batch limit ({policy.MaxBatchPerDay}) reached ({todayBatches} batches today)";
                decision.Decision = "Scheduled";
                decision.ScheduledFor = DateTime.UtcNow.Date.AddDays(1).AddHours(policy.AllowedStartHour ?? 0);
                _logger.LogInformation(
                    "Batch {BatchId} scheduled due to daily limit: {Reason}, scheduled for {ScheduledFor}",
                    batch.Id, decision.Reason, decision.ScheduledFor);
                return decision;
            }
        }

        return decision;
    }

    private static bool IsWithinAllowedHours(int currentHour, byte startHour, byte endHour)
    {
        if (startHour <= endHour)
        {
            return currentHour >= startHour && currentHour < endHour;
        }
        // Janela que cruza meia-noite (ex: 22-06)
        return currentHour >= startHour || currentHour < endHour;
    }

    private DateTime GetNextAllowedTime(ClientPolicy policy)
    {
        var now = DateTime.UtcNow;
        var nextAllowed = now;

        if (policy.AllowedStartHour.HasValue)
        {
            nextAllowed = now.Date.AddHours(policy.AllowedStartHour.Value);
            if (nextAllowed <= now)
            {
                nextAllowed = nextAllowed.AddDays(1);
            }
        }
        else
        {
            nextAllowed = now.AddHours(1);
        }

        return nextAllowed;
    }

    private async Task<int> CountBatchesTodayAsync(Guid clientId, CancellationToken cancellationToken)
    {
        // TODO: Implementar método específico no repositório se necessário
        // Por enquanto, retornar 0 para não bloquear
        return 0;
    }
}

