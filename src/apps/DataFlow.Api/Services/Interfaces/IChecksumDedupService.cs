using System;
using System.Threading.Tasks;

namespace DataFlow.Api.Services.Interfaces;

public interface IChecksumDedupService
{
    /// <summary>
    /// Se já existe um job associado ao checksum, retorna seu Guid.
    /// Caso contrário, cria uma reserva temporária e retorna null.
    /// </summary>
    Task<Guid?> GetExistingOrReserveAsync(string checksum, TimeSpan ttl);

    /// <summary>
    /// Associa o checksum ao jobId definitivo.
    /// </summary>
    Task AssociateAsync(string checksum, Guid jobId, TimeSpan ttl);

    /// <summary>
    /// Cancela a reserva temporária.
    /// </summary>
    Task ReleaseReservationAsync(string checksum);
}
