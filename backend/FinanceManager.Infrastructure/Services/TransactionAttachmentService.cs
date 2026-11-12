using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class TransactionAttachmentService : ITransactionAttachmentService
{
    public const long MaximumFileSize = 5 * 1024 * 1024;
    public const int MaximumPerTransaction = 10;

    private readonly FinanceDbContext _db;
    private readonly IFinancialTransactionRepository _transactions;

    public TransactionAttachmentService(FinanceDbContext db, IFinancialTransactionRepository transactions)
    {
        _db = db;
        _transactions = transactions;
    }

    public async Task<List<AttachmentResponse>?> ListAsync(Guid transactionId, Guid userId, CancellationToken ct = default)
    {
        if (await _transactions.GetByIdAsync(transactionId, userId, ct) is null)
            return null;

        return await _db.TransactionAttachments
            .AsNoTracking()
            .Where(x => x.TransactionId == transactionId)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new AttachmentResponse(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<AttachmentContent?> GetAsync(Guid transactionId, Guid attachmentId, Guid userId, CancellationToken ct = default)
    {
        if (await _transactions.GetByIdAsync(transactionId, userId, ct) is null)
            return null;

        return await _db.TransactionAttachments
            .AsNoTracking()
            .Where(x => x.Id == attachmentId && x.TransactionId == transactionId)
            .Select(x => new AttachmentContent(x.FileName, x.ContentType, x.Data))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<AttachmentResponse?> AddAsync(
        Guid transactionId,
        Guid userId,
        string fileName,
        Stream content,
        CancellationToken ct = default)
    {
        if (await _transactions.GetByIdAsync(transactionId, userId, ct) is null ||
            !await _transactions.CanEditTransactionAsync(transactionId, userId, ct))
        {
            return null;
        }

        var count = await _db.TransactionAttachments.CountAsync(x => x.TransactionId == transactionId, ct);
        if (count >= MaximumPerTransaction)
            throw new ArgumentException($"A transaction can have at most {MaximumPerTransaction} receipts.");

        using var buffer = new MemoryStream();
        await CopyLimitedAsync(content, buffer, ct);
        var data = buffer.ToArray();

        if (data.Length == 0)
            throw new ArgumentException("The file is empty.");

        var contentType = DetectContentType(data)
            ?? throw new ArgumentException("Receipts must be a JPEG, PNG, WebP or HEIC photo, or a PDF.");

        var attachment = new TransactionAttachment
        {
            TransactionId = transactionId,
            UploadedByUserId = userId,
            FileName = SafeFileName(fileName, contentType),
            ContentType = contentType,
            SizeBytes = data.Length,
            Data = data,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TransactionAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return new AttachmentResponse(
            attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.CreatedAtUtc);
    }

    public async Task<bool> DeleteAsync(Guid transactionId, Guid attachmentId, Guid userId, CancellationToken ct = default)
    {
        if (await _transactions.GetByIdAsync(transactionId, userId, ct) is null ||
            !await _transactions.CanEditTransactionAsync(transactionId, userId, ct))
        {
            return false;
        }

        var deleted = await _db.TransactionAttachments
            .Where(x => x.Id == attachmentId && x.TransactionId == transactionId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }

    private static async Task CopyLimitedAsync(Stream source, Stream destination, CancellationToken ct)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            total += read;
            if (total > MaximumFileSize)
                throw new ArgumentException("Receipts cannot exceed 5 MB.");
            await destination.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }

    // Identifies the file by its signature so a renamed HTML or script
    // file can never be served back as a receipt.
    internal static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
            return "image/jpeg";
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return "image/png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        if (data.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (data.Length >= 12 && data[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = data[8..12];
            if (brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) ||
                brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8))
            {
                return "image/heic";
            }
        }

        return null;
    }

    private static string SafeFileName(string fileName, string contentType)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        name = new string(name.Where(c => !char.IsControl(c) && c != '"' && c != '\\' && c != '/').ToArray()).Trim();

        if (name.Length == 0)
        {
            name = "receipt" + contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/heic" => ".heic",
                _ => ".pdf"
            };
        }

        return name.Length <= 200 ? name : name[^200..];
    }
}
