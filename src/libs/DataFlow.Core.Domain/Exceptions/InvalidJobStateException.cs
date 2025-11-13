namespace DataFlow.Core.Domain.Exceptions;

public class InvalidJobStateException : DomainException
{
    public Guid JobId { get; }
    public string CurrentStatus { get; }
    public string AttemptedOperation { get; }

    public InvalidJobStateException(Guid jobId, string currentStatus, string attemptedOperation)
        : base("INVALID_JOB_STATE", 
              $"Cannot perform '{attemptedOperation}' on job {jobId} in status '{currentStatus}'")
    {
        JobId = jobId;
        CurrentStatus = currentStatus;
        AttemptedOperation = attemptedOperation;
    }
}