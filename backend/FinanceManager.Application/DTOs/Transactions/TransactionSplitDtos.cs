namespace FinanceManager.Application.DTOs.Transactions;

public sealed record TransactionSplitResponse(
    Guid Id,
    Guid CategoryId,
    decimal Amount,
    string? Note);

public sealed record SplitLineRequest(
    Guid CategoryId,
    decimal Amount,
    string? Note);

// An empty list removes the split.
public sealed record SetSplitsRequest(
    List<SplitLineRequest> Lines);
