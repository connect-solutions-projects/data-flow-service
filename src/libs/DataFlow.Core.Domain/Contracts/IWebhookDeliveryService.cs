using DataFlow.Core.Domain.Entities;

namespace DataFlow.Core.Domain.Contracts;

public interface IWebhookDeliveryService
{
    /// <summary>
    /// Envia webhook para todas as subscriptions ativas do cliente quando um batch é finalizado.
    /// </summary>
    Task DeliverBatchFinalizedAsync(ImportBatch batch, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registra falha de entrega para reprocesso manual.
    /// </summary>
    Task RecordDeliveryFailureAsync(Guid webhookId, string error, CancellationToken cancellationToken = default);
}

