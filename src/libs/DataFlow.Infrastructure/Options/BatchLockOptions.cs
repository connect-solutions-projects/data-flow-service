namespace DataFlow.Infrastructure.Options;

public class BatchLockOptions
{
    public const string SectionName = "BatchLock";
    public string Provider { get; set; } = "SqlServer"; // "SqlServer" ou "Redis"
    public TimeSpan? RedisLockTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

