using DataFlow.Core.Domain.Contracts;
using Microsoft.Extensions.Logging;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System.Net;

namespace DataFlow.Infrastructure.Repositories;

public class RedisBatchLockRepository : IBatchLockRepository
{
    private readonly IDistributedLockFactory _redLockFactory;
    private readonly ILogger<RedisBatchLockRepository> _logger;
    private const string LockKey = "dataflow:batch:lock";
    private IRedLock? _currentLock;
    private DateTime? _lockAcquiredAt;

    public RedisBatchLockRepository(
        IConnectionMultiplexer redis,
        ILogger<RedisBatchLockRepository> logger)
    {
        _logger = logger;
        var endpoints = new List<RedLockEndPoint>();
        foreach (var endpoint in redis.GetEndPoints())
        {
            if (endpoint is DnsEndPoint dns)
                endpoints.Add(new DnsEndPoint(dns.Host, dns.Port));
            else if (endpoint is IPEndPoint ip)
                endpoints.Add(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(ip.Address.ToString()), ip.Port));
        }
        _redLockFactory = RedLockFactory.Create(endpoints);
    }

    public async Task<bool> TryAcquireLockAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Tenta adquirir lock com timeout de 30 segundos e expiração de 30 minutos
            var expiry = TimeSpan.FromMinutes(30);
            var wait = TimeSpan.FromSeconds(30);
            var retry = TimeSpan.FromSeconds(1);

            _currentLock = await _redLockFactory.CreateLockAsync(
                LockKey,
                expiry,
                wait,
                retry,
                cancellationToken);

            if (_currentLock.IsAcquired)
            {
                _lockAcquiredAt = DateTime.UtcNow;
                _logger.LogInformation("Lock acquired for batch {BatchId}", batchId);
                return true;
            }

            _logger.LogDebug("Could not acquire lock for batch {BatchId}", batchId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock for batch {BatchId}", batchId);
            return false;
        }
    }

    public async Task ReleaseLockAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentLock?.IsAcquired == true)
            {
                await _currentLock.DisposeAsync();
                _logger.LogInformation("Lock released");
            }
            _currentLock = null;
            _lockAcquiredAt = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock");
        }
    }

    public async Task<bool> IsLockExpiredAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // Redis locks expiram automaticamente via TTL
        // Verificar se existe uma instância de lock ativa
        return _currentLock?.IsAcquired != true;
    }

    public async Task ForceReleaseExpiredLocksAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // Redis locks expiram automaticamente, mas podemos verificar e limpar manualmente
        try
        {
            if (_currentLock?.IsAcquired == true && _lockAcquiredAt.HasValue)
            {
                var lockAge = DateTime.UtcNow - _lockAcquiredAt.Value;
                if (lockAge > timeout)
                {
                    _logger.LogWarning("Force releasing expired lock (age: {Age})", lockAge);
                    await ReleaseLockAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in force release expired locks");
        }
        await Task.CompletedTask;
    }
}

