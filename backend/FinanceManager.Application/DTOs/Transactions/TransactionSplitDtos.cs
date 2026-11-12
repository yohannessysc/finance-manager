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

// Sets one category (or none) on many transactions at once.
public sealed record BulkCategoryRequest(
    List<Guid> TransactionIds,
    Guid? CategoryId);

public sealed record BulkCategoryResponse(
    int Updated,
    // Transfers, other-type transactions and ones the user cannot edit.
    int Skipped);
