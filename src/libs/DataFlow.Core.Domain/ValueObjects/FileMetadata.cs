namespace DataFlow.Core.Domain.ValueObjects;

public record FileMetadata
{
    public string Name { get; init; }
    public long Size { get; init; }
    public string ContentType { get; init; }
    public string Checksum { get; init; }
    public DateTime UploadedAt { get; init; }

    public FileMetadata(string name, long size, string contentType, string checksum, DateTime uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("File name cannot be empty", nameof(name));
        
        if (size <= 0)
            throw new ArgumentException("File size must be greater than zero", nameof(size));
        
        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum cannot be empty", nameof(checksum));

        Name = name;
        Size = size;
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        Checksum = checksum;
        UploadedAt = uploadedAt;
    }

    public bool IsLargeFile(long thresholdInBytes = 100 * 1024 * 1024) => Size > thresholdInBytes;
}