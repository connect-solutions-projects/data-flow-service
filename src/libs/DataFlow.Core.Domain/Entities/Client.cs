using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.Entities;

public class Client
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ClientIdentifier { get; private set; } = string.Empty;
    public byte[] SecretHash { get; private set; } = Array.Empty<byte>();
    public byte[] SecretSalt { get; private set; } = Array.Empty<byte>();
    public ClientStatus Status { get; private set; } = ClientStatus.Active;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }

    // Navigation
    public ICollection<ClientPolicy> Policies { get; private set; } = new List<ClientPolicy>();
    public ICollection<WebhookSubscription> WebhookSubscriptions { get; private set; } = new List<WebhookSubscription>();
    public ICollection<ImportBatch> ImportBatches { get; private set; } = new List<ImportBatch>();

    private Client() { }

    public Client(string name, string clientIdentifier)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Client name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(clientIdentifier))
            throw new ArgumentException("Client identifier cannot be empty.", nameof(clientIdentifier));

        Id = Guid.NewGuid();
        Name = name.Trim();
        ClientIdentifier = clientIdentifier.Trim().ToLowerInvariant();
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateSecret(byte[] hash, byte[] salt)
    {
        SecretHash = hash ?? throw new ArgumentNullException(nameof(hash));
        SecretSalt = salt ?? throw new ArgumentNullException(nameof(salt));
    }

    public void Touch()
    {
        LastSeenAt = DateTime.UtcNow;
    }

    public void Suspend() => Status = ClientStatus.Suspended;
    public void Activate() => Status = ClientStatus.Active;
}

