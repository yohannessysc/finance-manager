
using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Services;

public class FinancialTransactionService
    : IFinancialTransactionService
{
    private readonly IFinancialTransactionRepository _repository;

    public FinancialTransactionService(
        IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TransactionResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var transactions = await _repository.GetAllAsync(
            userId, ct);
        var splits = (await _repository.GetSplitsAsync(
                transactions.Select(x => x.Id).ToList(), ct))
            .ToLookup(x => x.TransactionId);

        return transactions
            .Select(x => MapToResponse(x, splits[x.Id]))
            .ToList();
    }

    public async Task<TransactionResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var transaction = await _repository.GetByIdAsync(
            id, userId, ct);

        if (transaction is null)
            return null;

        var splits = await _repository.GetSplitsAsync([transaction.Id], ct);
        return MapToResponse(transaction, splits);
    }

    public async Task<TransactionResponse> CreateAsync(
        Guid userId,
        CreateTransactionRequest request,
        CancellationToken ct = default)
    {
        await ValidateAsync(
            userId,
            request.FinancialAccountId,
            request.DestinationAccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.Description,
            request.Merchant,
            request.TransactionDateUtc,
            ct);

        var transaction = new FinancialTransaction
        {
            UserId = userId,
            FinancialAccountId = request.FinancialAccountId,
            DestinationAccountId = request.DestinationAccountId,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Merchant = request.Merchant?.Trim(),
            TransactionDateUtc = request.TransactionDateUtc,
            CreatedAtUtc = DateTime.UtcNow,

            // Manual transactions do not belong to a schedule.
            RecurringTransactionId = null,
            ScheduledOccurrenceUtc = null
        };

        await _repository.AddAsync(transaction, ct);

        return MapToResponse(transaction);
    }

    public async Task<TransactionResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateTransactionRequest request,
        CancellationToken ct = default)
    {
        if (!await _repository.CanEditTransactionAsync(id, userId, ct))
            return null;

        var transaction = await _repository.GetByIdAsync(
            id, userId, ct);

        if (transaction is null)
        {
            return null;
        }

        EnsureManualTransaction(transaction);

        await ValidateAsync(
            userId,
            request.FinancialAccountId,
            request.DestinationAccountId,
            request.CategoryId,
            request.Type,
            request.Amount,
            request.Description,
            request.Merchant,
            request.TransactionDateUtc,
            ct);

        var splits = await _repository.GetSplitsAsync([transaction.Id], ct);
        if (splits.Count > 0)
        {
            if (request.CategoryId is not null)
            {
                // Choosing a single category replaces the split.
                await _repository.ReplaceSplitsAsync(transaction.Id, [], ct);
                splits = [];
            }
            else if (request.Amount != transaction.Amount ||
                     request.Type != transaction.Type)
            {
                throw new ArgumentException(
                    "This transaction is split. Remove the split before changing its amount or type.");
            }
        }

        transaction.FinancialAccountId =
            request.FinancialAccountId;

        transaction.DestinationAccountId =
            request.DestinationAccountId;

        transaction.CategoryId = request.CategoryId;
        transaction.Type = request.Type;
        transaction.Amount = request.Amount;
        transaction.Description = request.Description.Trim();
        transaction.Merchant = request.Merchant?.Trim();
        transaction.TransactionDateUtc =
            request.TransactionDateUtc;

        await _repository.SaveChangesAsync(ct);

        return MapToResponse(transaction, splits);
    }

    public async Task<TransactionResponse?> SetSplitsAsync(
        Guid id,
        Guid userId,
        SetSplitsRequest request,
        CancellationToken ct = default)
    {
        if (!await _repository.CanEditTransactionAsync(id, userId, ct))
            return null;

        var transaction = await _repository.GetByIdAsync(id, userId, ct);
        if (transaction is null)
            return null;

        var lines = request.Lines ?? [];
        var splits = new List<TransactionSplit>();

        if (lines.Count > 0)
        {
            if (transaction.Type == TransactionType.Transfer)
                throw new ArgumentException("Transfers cannot be split across categories.");
            if (lines.Count < 2)
                throw new ArgumentException("A split needs at least two lines.");
            if (lines.Count > MaximumSplitLines)
                throw new ArgumentException($"A split can have at most {MaximumSplitLines} lines.");

            foreach (var line in lines)
            {
                if (line.Amount <= 0 || decimal.Round(line.Amount, 2) != line.Amount)
                    throw new ArgumentException("Each split amount must be positive with at most two decimal places.");
                if (line.Note?.Trim().Length > 200)
                    throw new ArgumentException("Split notes cannot exceed 200 characters.");

                var category = await _repository.GetCategoryAsync(line.CategoryId, userId, ct);
                if (category is null || category.Type != transaction.Type)
                    throw new ArgumentException("Each split line needs a category of the transaction's type.");

                splits.Add(new TransactionSplit
                {
                    TransactionId = transaction.Id,
                    CategoryId = line.CategoryId,
                    Amount = line.Amount,
                    Note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim()
                });
            }

            if (splits.Sum(x => x.Amount) != transaction.Amount)
            {
                throw new ArgumentException(
                    $"Split lines must add up to the transaction amount ({transaction.Amount:0.00}).");
            }

            // The lines carry the categories now.
            transaction.CategoryId = null;
        }

        await _repository.ReplaceSplitsAsync(transaction.Id, splits, ct);
        await _repository.SaveChangesAsync(ct);

        return MapToResponse(transaction, splits);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!await _repository.CanEditTransactionAsync(id, userId, ct))
            return false;

        var transaction = await _repository.GetByIdAsync(
            id, userId, ct);

        if (transaction is null)
        {
            return false;
        }

        EnsureManualTransaction(transaction);

        await _repository.DeleteAsync(transaction, ct);

        return true;
    }

    private async Task ValidateAsync(
        Guid userId,
        Guid accountId,
        Guid? destinationAccountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string? description,
        string? merchant,
        DateTime transactionDateUtc,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException(
                "Invalid transaction type.");
        }

        if (amount <= 0 || amount > 9999999999999999.99m)
        {
            throw new ArgumentException(
                "Amount must be positive and fit within 16 digits before the decimal.");
        }

        if (description?.Trim().Length > 500)
        {
            throw new ArgumentException(
                "Description cannot exceed 500 characters.");
        }

        if (merchant?.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Merchant cannot exceed 200 characters.");
        }

        if (transactionDateUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Transaction date must be UTC, ending with Z.");
        }

        var account = await _repository.GetAccountAsync(
            accountId, userId, ct);

        if (account is null)
        {
            throw new ArgumentException(
                "Invalid financial account.");
        }

        if (!await _repository.CanEditAccountAsync(
                accountId,
                userId,
                ct))
        {
            throw new UnauthorizedAccessException(
                "Your household role does not allow transaction changes.");
        }

        if (type == TransactionType.Transfer)
        {
            if (categoryId is not null)
            {
                throw new ArgumentException(
                    "Transfers cannot have an income or expense category.");
            }

            if (destinationAccountId is null ||
                destinationAccountId == accountId)
            {
                throw new ArgumentException(
                    "Transfers require a different destination account.");
            }

            var destination = await _repository.GetAccountAsync(
                destinationAccountId.Value, userId, ct);

            if (destination is null)
            {
                throw new ArgumentException(
                    "Invalid destination account.");
            }

            if (!await _repository.CanEditAccountAsync(
                    destinationAccountId.Value,
                    userId,
                    ct))
            {
                throw new UnauthorizedAccessException(
                    "Your household role does not allow transaction changes.");
            }

            if (account.Currency != destination.Currency)
            {
                throw new ArgumentException(
                    "Transfers between different currencies are not supported yet.");
            }
        }
        else
        {
            if (destinationAccountId is not null)
            {
                throw new ArgumentException(
                    "Only transfers can have a destination account.");
            }

            if (categoryId is not null)
            {
                var category = await _repository.GetCategoryAsync(
                    categoryId.Value, userId, ct);

                if (category is null || category.Type != type)
                {
                    throw new ArgumentException(
                        "Invalid category for this transaction type.");
                }
            }
        }
    }

    private static void EnsureManualTransaction(
        FinancialTransaction transaction)
    {
        if (transaction.RecurringTransactionId is not null)
        {
            throw new ArgumentException(
                "Generated recurring transactions cannot be edited or deleted here.");
        }
    }

    private const int MaximumSplitLines = 20;

    private static TransactionResponse MapToResponse(
        FinancialTransaction transaction,
        IEnumerable<TransactionSplit>? splits = null)
    {
        var lines = splits?
            .Select(x => new TransactionSplitResponse(x.Id, x.CategoryId, x.Amount, x.Note))
            .ToList();

        return new TransactionResponse(
            transaction.Id,
            transaction.FinancialAccountId,
            transaction.DestinationAccountId,
            transaction.CategoryId,
            transaction.RecurringTransactionId,
            transaction.Type,
            transaction.Amount,
            transaction.Description,
            transaction.Merchant,
            transaction.TransactionDateUtc,
            transaction.CreatedAtUtc,
            lines is { Count: > 0 } ? lines : null
        );
    }
}
