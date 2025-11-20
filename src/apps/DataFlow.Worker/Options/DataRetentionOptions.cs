namespace DataFlow.Worker.Options;

public class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    public bool EnableCleanup { get; set; } = true;
    public int BatchRetentionDays { get; set; } = 30;
    public int CheckIntervalHours { get; set; } = 6;
    public int MaxBatchesPerRun { get; set; } = 100;
}

