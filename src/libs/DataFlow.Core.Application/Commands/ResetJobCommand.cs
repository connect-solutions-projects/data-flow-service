using DataFlow.Core.Application.DTOs;
using MediatR;

namespace DataFlow.Core.Application.Commands;

public record ResetJobCommand : IRequest<JobResponse>
{
    public required Guid JobId { get; init; }
}
