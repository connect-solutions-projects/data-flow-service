namespace DataFlow.Core.Domain.Entities;

public class ClientPolicy
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public int? MaxFileSizeMb { get; private set; }
    public int? MaxBatchPerDay { get; private set; }
    public byte? AllowedStartHour { get; private set; }
    public byte? AllowedEndHour { get; private set; }
    public bool RequireSchedulingForLarge { get; private set; }
    public int? LargeThresholdMb { get; private set; }
    public int? RateLimitPerMinute { get; private set; }
    public bool? RedactPayloadOnSuccess { get; private set; }
    public bool? RedactPayloadOnFailure { get; private set; }
    public int? RetentionDays { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Client Client { get; private set; } = null!;

    private ClientPolicy() { }

    public ClientPolicy(Guid clientId)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException("ClientId cannot be empty.", nameof(clientId));

        Id = Guid.NewGuid();
        ClientId = clientId;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateRateLimit(int? rateLimitPerMinute)
    {
        RateLimitPerMinute = rateLimitPerMinute;
    }

    public void UpdatePrivacySettings(bool? redactOnSuccess, bool? redactOnFailure, int? retentionDays)
    {
        RedactPayloadOnSuccess = redactOnSuccess;
        RedactPayloadOnFailure = redactOnFailure;
        RetentionDays = retentionDays;
    }
}

