using System;
using System.Threading.Tasks;

namespace DataFlow.Api.Services.Interfaces;

public sealed class RateLimitDecision
{
    public bool IsAllowed { get; init; }
    public int Limit { get; init; }
    public int Remaining { get; init; }
    public int? RetryAfterSeconds { get; init; }
}

public interface IRedisRateLimiter
{
    /// <summary>
    /// Tenta consumir 1 unidade do limite para a chave informada.
    /// Retorna detalhes para cabeçalhos de rate limit.
    /// </summary>
    Task<RateLimitDecision> AllowAsync(string key, int limit, TimeSpan period);
}
