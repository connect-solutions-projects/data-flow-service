using DataFlow.Core.Application.DTOs;
using DataFlow.Core.Domain.Enums;
using MediatR;

namespace DataFlow.Core.Application.Queries;

public record GetJobsByStatusQuery : IRequest<IReadOnlyList<JobResponse>>
{
    public required JobStatus Status { get; init; }
    public int? Limit { get; init; }
}