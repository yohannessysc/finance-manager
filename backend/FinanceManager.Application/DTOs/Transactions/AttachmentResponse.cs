namespace FinanceManager.Application.DTOs.Transactions;

public sealed record AttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);

public sealed record AttachmentContent(
    string FileName,
    string ContentType,
    byte[] Data);
