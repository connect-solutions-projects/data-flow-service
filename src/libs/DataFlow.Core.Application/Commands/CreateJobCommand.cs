using DataFlow.Core.Application.DTOs;
using MediatR;

namespace DataFlow.Core.Application.Commands;

public record CreateJobCommand : IRequest<JobResponse>
{
    public required string ClientId { get; init; }
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required string ContentType { get; init; }
    public required string Checksum { get; init; }
    public required string FileType { get; init; }
}
