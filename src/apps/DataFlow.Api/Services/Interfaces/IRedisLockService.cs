using System;
using System.Threading.Tasks;

namespace DataFlow.Api.Services.Interfaces;

public interface IRedisLockService
{
    /// <summary>
    /// Tenta adquirir um lock distribuído. Retorna o token do lock se adquirido; caso contrário, null.
    /// </summary>
    Task<string?> AcquireAsync(string key, TimeSpan ttl);

    /// <summary>
    /// Libera o lock distribuído usando o token de posse.
    /// </summary>
    Task<bool> ReleaseAsync(string key, string token);
}

