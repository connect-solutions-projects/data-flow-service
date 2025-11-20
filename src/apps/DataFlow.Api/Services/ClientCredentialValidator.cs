using DataFlow.Api.Services.Interfaces;
using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Infrastructure.Persistence;
using DataFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataFlow.Api.Services;

public class ClientCredentialValidator : IClientCredentialValidator
{
    private readonly IngestionDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ClientCredentialValidator> _logger;
    private const int CacheTtlMinutes = 5;

    public ClientCredentialValidator(
        IngestionDbContext dbContext, 
        IDistributedCache cache,
        ILogger<ClientCredentialValidator> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Client?> ValidateAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Headers.TryGetValue("X-Client-Id", out var clientIdHeader) ||
            !request.Headers.TryGetValue("X-Client-Secret", out var secretHeader))
        {
            return null;
        }

        var identifier = clientIdHeader.ToString().Trim().ToLowerInvariant();
        var secret = secretHeader.ToString();

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(secret))
            return null;

        // Tentar buscar do cache primeiro
        var cacheKey = $"client:{identifier}";
        var cachedJson = await _cache.GetStringAsync(cacheKey, cancellationToken);
        Client? client = null;

        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            try
            {
                var cachedData = JsonSerializer.Deserialize<ClientCacheData>(cachedJson);
                if (cachedData != null)
                {
                    // Buscar do banco usando ID do cache
                    client = await _dbContext.Clients
                        .Include(c => c.Policies)
                        .FirstOrDefaultAsync(c => c.Id == cachedData.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deserializing cached client {Identifier}", identifier);
            }
        }

        // Se não encontrou no cache, buscar do banco
        if (client == null)
        {
            client = await _dbContext.Clients
                .Include(c => c.Policies)
                .FirstOrDefaultAsync(c => c.ClientIdentifier == identifier, cancellationToken);
            
            if (client is null)
            {
                _logger.LogWarning("Unknown client identifier {Identifier}", identifier);
                return null;
            }

            // Cachear o cliente
            try
            {
                var cacheData = new ClientCacheData { Id = client.Id, ClientIdentifier = client.ClientIdentifier };
                var cacheJson = JsonSerializer.Serialize(cacheData);
                await _cache.SetStringAsync(
                    cacheKey, 
                    cacheJson, 
                    new DistributedCacheEntryOptions 
                    { 
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheTtlMinutes) 
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching client {Identifier}", identifier);
            }
        }

        if (client.Status != ClientStatus.Active)
        {
            _logger.LogWarning("Client {Identifier} is not active.", identifier);
            return null;
        }

        if (client.SecretHash is null || client.SecretSalt is null ||
            !ClientSecretHasher.Verify(secret, client.SecretHash, client.SecretSalt))
        {
            _logger.LogWarning("Invalid secret for client {Identifier}", identifier);
            return null;
        }

        client.Touch();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return client;
    }

    private class ClientCacheData
    {
        public Guid Id { get; set; }
        public string ClientIdentifier { get; set; } = string.Empty;
    }
}

