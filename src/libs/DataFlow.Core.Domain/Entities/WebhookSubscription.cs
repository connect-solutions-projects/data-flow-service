namespace DataFlow.Core.Domain.Entities;

public class WebhookSubscription
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? Secret { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    public Client Client { get; private set; } = null!;

    private WebhookSubscription() { }

    public WebhookSubscription(Guid clientId, string url, string? secret = null)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException("ClientId cannot be empty.", nameof(clientId));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Webhook url cannot be empty.", nameof(url));

        Id = Guid.NewGuid();
        ClientId = clientId;
        Url = url;
        Secret = secret;
        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}

