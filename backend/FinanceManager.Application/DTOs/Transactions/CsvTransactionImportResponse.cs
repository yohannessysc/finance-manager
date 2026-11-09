namespace FinanceManager.Application.DTOs.Transactions;

public sealed record CsvTransactionImportResponse(
    int Imported,
    int Duplicates,
    int InvalidRows,
    List<CsvTransactionImportError> Errors);

public sealed record CsvTransactionImportError(
    int Row,
    string Message);
