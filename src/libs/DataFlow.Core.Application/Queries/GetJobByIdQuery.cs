using DataFlow.Core.Application.DTOs;
using MediatR;

namespace DataFlow.Core.Application.Queries;

public record GetJobByIdQuery : IRequest<JobResponse?>
{
    public required Guid JobId { get; init; }
}