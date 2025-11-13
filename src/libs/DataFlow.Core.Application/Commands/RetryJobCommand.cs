using DataFlow.Core.Application.DTOs;
using MediatR;

namespace DataFlow.Core.Application.Commands;

public record RetryJobCommand : IRequest<JobResponse>
{
    public required Guid JobId { get; init; }
}