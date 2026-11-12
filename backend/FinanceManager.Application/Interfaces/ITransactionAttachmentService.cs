using FinanceManager.Application.DTOs.Transactions;

namespace FinanceManager.Application.Interfaces;

// Receipts on transactions. Methods return null when the transaction or
// attachment does not exist or the user cannot see or edit it.
public interface ITransactionAttachmentService
{
    Task<List<AttachmentResponse>?> ListAsync(Guid transactionId, Guid userId, CancellationToken ct = default);

    Task<AttachmentContent?> GetAsync(Guid transactionId, Guid attachmentId, Guid userId, CancellationToken ct = default);

    // Throws ArgumentException for unsupported, empty or oversized files.
    Task<AttachmentResponse?> AddAsync(Guid transactionId, Guid userId, string fileName, Stream content, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid transactionId, Guid attachmentId, Guid userId, CancellationToken ct = default);
}
