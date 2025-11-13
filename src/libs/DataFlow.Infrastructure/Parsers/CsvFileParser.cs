using System.Text;
using DataFlow.Core.Domain.Contracts;
using DataFlow.Core.Domain.Enums;

namespace DataFlow.Infrastructure.Parsers;

public class CsvFileParser : IFileParser
{
    public FileType SupportedType => FileType.Csv;
    public string ContentType => "text/csv";

    public async IAsyncEnumerable<T> ParseAsync<T>(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
    {
        // Minimal stub: return empty sequence; handlers currently don't rely on ParseAsync
        await Task.CompletedTask;
        yield break;
    }

    public async Task<int> CountRecordsAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        int count = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (line != null) count++;
        }
        return count;
    }

    public bool CanParse(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ".csv" || contentType.Equals(ContentType, StringComparison.OrdinalIgnoreCase);
    }
}
