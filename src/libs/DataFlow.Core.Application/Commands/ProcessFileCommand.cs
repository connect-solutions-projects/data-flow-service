using DataFlow.Core.Application.DTOs;
using MediatR;

namespace DataFlow.Core.Application.Commands;

public record ProcessFileCommand : IRequest<ValidationResultDto>
{
    public required Guid JobId { get; init; }
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
}