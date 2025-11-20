namespace DataFlow.Core.Domain.Enums;

public enum ImportBatchStatus
{
    Pending = 1,
    Processing = 2,
    Scheduled = 3,
    Completed = 4,
    CompletedWithErrors = 5,
    Failed = 6
}

