using System.Security.Cryptography;
using System.Text;
using DataFlow.Core.Domain.Enums;

namespace DataFlow.Core.Domain.Entities;

public class ImportItem
{
    public long Id { get; private set; }
    public Guid BatchId { get; private set; }
    public int Sequence { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public ImportItemStatus Status { get; private set; } = ImportItemStatus.Imported;
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ImportBatch Batch { get; private set; } = null!;

    private ImportItem() { }

    public ImportItem(Guid batchId, int sequence, string payloadJson)
    {
        if (batchId == Guid.Empty)
            throw new ArgumentException("BatchId cannot be empty.", nameof(batchId));
        if (sequence < 0)
            throw new ArgumentException("Sequence cannot be negative.", nameof(sequence));
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("PayloadJson cannot be empty.", nameof(payloadJson));

        BatchId = batchId;
        Sequence = sequence;
        PayloadJson = payloadJson;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkError(string message)
    {
        Status = ImportItemStatus.Error;
        ErrorMessage = message;
    }

    public void MarkImported()
    {
        Status = ImportItemStatus.Imported;
        ErrorMessage = null;
    }

    public void RedactPayload(bool includeHash = true)
    {
        if (string.IsNullOrWhiteSpace(PayloadJson))
            return;

        if (includeHash)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(PayloadJson));
            var hash = Convert.ToHexString(hashBytes);
            PayloadJson = $"{{\"masked\":true,\"sha256\":\"{hash}\"}}";
        }
        else
        {
            PayloadJson = "{\"masked\":true}";
        }
    }
}

