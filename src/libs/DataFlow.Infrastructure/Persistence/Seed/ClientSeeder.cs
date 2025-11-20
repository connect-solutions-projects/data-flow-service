using DataFlow.Core.Domain.Entities;
using DataFlow.Core.Domain.Enums;
using DataFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataFlow.Infrastructure.Persistence.Seed;

public class ClientSeeder
{
    private readonly IngestionDbContext _dbContext;
    private readonly ILogger<ClientSeeder> _logger;
    private readonly ClientSeedOptions _options;

    public ClientSeeder(
        IngestionDbContext dbContext,
        IOptions<ClientSeedOptions> options,
        ILogger<ClientSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Clients is null || _options.Clients.Count == 0)
        {
            _logger.LogInformation("Client seed skipped: no entries configured.");
            return;
        }

        foreach (var entry in _options.Clients)
        {
            if (string.IsNullOrWhiteSpace(entry.ClientIdentifier) || string.IsNullOrWhiteSpace(entry.Name))
            {
                _logger.LogWarning("Skipping client seed entry with missing name/identifier.");
                continue;
            }

            var normalizedId = entry.ClientIdentifier.Trim().ToLowerInvariant();
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.ClientIdentifier == normalizedId, cancellationToken);
            if (client is null)
            {
                client = new Client(entry.Name, normalizedId);
                if (!string.IsNullOrWhiteSpace(entry.Secret))
                {
                    var (hash, salt) = ClientSecretHasher.HashSecret(entry.Secret);
                    client.UpdateSecret(hash, salt);
                }
                else
                {
                    _logger.LogWarning("Client '{ClientIdentifier}' has no secret configured; skipping.", normalizedId);
                    continue;
                }

                await _dbContext.Clients.AddAsync(client, cancellationToken);
                _logger.LogInformation("Seeded client '{ClientIdentifier}'.", normalizedId);
            }
            else if (!string.IsNullOrWhiteSpace(entry.Secret))
            {
                var (hash, salt) = ClientSecretHasher.HashSecret(entry.Secret);
                client.UpdateSecret(hash, salt);
                client.Activate();
                _dbContext.Clients.Update(client);
                _logger.LogInformation("Client '{ClientIdentifier}' secret rotated from seed.", normalizedId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

