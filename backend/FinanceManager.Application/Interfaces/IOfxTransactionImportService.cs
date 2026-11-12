using FinanceManager.Application.DTOs.Transactions;

namespace FinanceManager.Application.Interfaces;

public interface IOfxTransactionImportService
{
    // Imports an OFX/QFX statement into one of the user's accounts.
    // Already-imported transactions (same bank id) are skipped.
    Task<CsvTransactionImportResponse> ImportAsync(
        Guid userId,
        Guid accountId,
        Stream stream,
        CancellationToken ct = default);
}
