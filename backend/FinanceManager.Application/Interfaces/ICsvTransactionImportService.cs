using FinanceManager.Application.DTOs.Transactions;

namespace FinanceManager.Application.Interfaces;

public interface ICsvTransactionImportService
{
    Task<CsvTransactionImportResponse> ImportAsync(
        Guid userId,
        Stream csvStream,
        CancellationToken ct = default);
}
