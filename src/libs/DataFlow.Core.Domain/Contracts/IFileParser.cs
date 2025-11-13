using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.Contracts;

public interface IFileParser
{
    FileType SupportedType { get; }
    string ContentType { get; }
    
    IAsyncEnumerable<T> ParseAsync<T>(
        Stream stream, 
        CancellationToken cancellationToken = default) where T : class, new();

    Task<int> CountRecordsAsync(
        Stream stream, 
        CancellationToken cancellationToken = default);

    bool CanParse(string fileName, string contentType);
}