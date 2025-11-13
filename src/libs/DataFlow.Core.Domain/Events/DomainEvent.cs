namespace DataFlow.Core.Domain.Events;

public abstract record DomainEvent(DateTime OccurredOn);

public record JobCreated(Guid JobId, string ClientId, string FileName, DateTime Timestamp) 
    : DomainEvent(Timestamp);

public record JobStarted(Guid JobId, DateTime Timestamp) 
    : DomainEvent(Timestamp);

public record JobCompleted(
    Guid JobId, 
    int TotalRecords, 
    int ValidRecords, 
    TimeSpan ProcessingTime, 
    DateTime Timestamp) 
    : DomainEvent(Timestamp);

public record JobFailed(
    Guid JobId, 
    string Reason, 
    int RetryCount, 
    DateTime Timestamp) 
    : DomainEvent(Timestamp);

public record JobRetried(Guid JobId, int NewRetryCount, DateTime Timestamp) 
    : DomainEvent(Timestamp);

public record ValidationCompleted(
    Guid JobId, 
    bool IsValid, 
    int ErrorCount, 
    int WarningCount, 
    DateTime Timestamp) 
    : DomainEvent(Timestamp);