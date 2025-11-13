using DataFlow.Core.Application.Interfaces;

namespace DataFlow.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath = Path.Combine(Path.GetTempPath(), "dataflow_uploads");

    public LocalFileStorageService()
    {
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_basePath, safeName);
        using var file = File.Create(fullPath);
        await fileStream.CopyToAsync(file, cancellationToken);
        return fullPath;
    }

    public Task<Stream> DownloadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Se receber um caminho absoluto, usa diretamente; caso contrário,
        // resolve dentro do diretório base de uploads.
        var resolvedPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(_basePath, Path.GetFileName(filePath));

        Console.WriteLine($"[LocalFileStorageService] Resolvido caminho para download: {resolvedPath}");

        Stream stream = File.OpenRead(resolvedPath);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(filePath));

    public Task<string> GetFileUrlAsync(string filePath, TimeSpan expiration, CancellationToken cancellationToken = default)
        => Task.FromResult(filePath);

    public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(_basePath)
            .Select(f => Path.GetFileName(f)!)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(files);
    }
}
