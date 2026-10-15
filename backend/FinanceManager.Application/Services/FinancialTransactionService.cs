
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

        return transactions.Select(MapToResponse).ToList();
    }

    public async Task<TransactionResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var transaction = await _repository.GetByIdAsync(
            id, userId, ct);

        return transaction is null
            ? null
            : MapToResponse(transaction);
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

        return MapToResponse(transaction);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
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

    private static TransactionResponse MapToResponse(
        FinancialTransaction transaction)
    {
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
            transaction.CreatedAtUtc
        );
    }
}
