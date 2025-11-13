namespace DataFlow.Api.Options;

public class RateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 30;
    public Dictionary<string, int> Tiers { get; set; } = new();
}

public static class RateLimitOptionKeys
{
    public const string SectionName = "RateLimit";
}
