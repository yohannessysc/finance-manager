namespace FinanceManager.Domain.Entities;

// A receipt photo or PDF attached to a transaction. Stored in the
// database so it is backed up and deleted together with the transaction.
public class TransactionAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TransactionId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string FileName { get; set; } = string.Empty;

    // Detected from the file's contents, not the browser's claim.
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public byte[] Data { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
