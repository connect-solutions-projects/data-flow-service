using System;
using System.Threading.Tasks;
using DataFlow.Api.Services.Interfaces;
using StackExchange.Redis;

namespace DataFlow.Api.Services;

public class ChecksumDedupService : IChecksumDedupService
{
    private readonly IConnectionMultiplexer _mux;
    public ChecksumDedupService(IConnectionMultiplexer mux)
    {
        _mux = mux;
    }

    private static string Key(string checksum) => $"checksum:{checksum}";

    public async Task<Guid?> GetExistingOrReserveAsync(string checksum, TimeSpan ttl)
    {
        var db = _mux.GetDatabase();
        var key = Key(checksum);
        var existing = await db.StringGetAsync(key);
        if (existing.HasValue)
        {
            if (Guid.TryParse(existing.ToString(), out var jobId))
                return jobId;
            // Se estava um placeholder, considerar como reserva já ativa
            return Guid.Empty; // indica reserva em progresso
        }

        // Cria placeholder de reserva
        var reserved = await db.StringSetAsync(key, "RESERVED", ttl, When.NotExists);
        return reserved ? null : Guid.Empty;
    }

    public async Task AssociateAsync(string checksum, Guid jobId, TimeSpan ttl)
    {
        var db = _mux.GetDatabase();
        await db.StringSetAsync(Key(checksum), jobId.ToString(), ttl);
    }

    public async Task ReleaseReservationAsync(string checksum)
    {
        var db = _mux.GetDatabase();
        await db.KeyDeleteAsync(Key(checksum));
    }
}
