using DataFlow.Core.Domain.Entities;

namespace DataFlow.Api.Services.Interfaces;

public interface IClientCredentialValidator
{
    Task<Client?> ValidateAsync(HttpRequest request, CancellationToken cancellationToken = default);
}

