
using FinanceManager.Application.DTOs.Accounts;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Services;

public class FinancialAccountService : IFinancialAccountService
{
    private readonly IFinancialAccountRepository _repository;

    private static readonly HashSet<string> ValidAccountTypes =
        new(StringComparer.Ordinal)
        {
            "Chequing",
            "Savings",
            "Cash",
            "CreditCard"
        };

    public FinancialAccountService(
        IFinancialAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<FinancialAccountResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var accounts = await _repository.GetAllAsync(userId, ct);

        return accounts.Select(MapToResponse).ToList();
    }

    public async Task<FinancialAccountResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var account = await _repository.GetByIdAsync(
            id, userId, ct);

        return account is null
            ? null
            : MapToResponse(account);
    }

    public async Task<FinancialAccountResponse> CreateAsync(
        Guid userId,
        CreateAccountRequest request,
        CancellationToken ct = default)
    {
        ValidateAccount(request.Name, request.AccountType);

        var account = new FinancialAccount
        {
            UserId = userId,
            Name = request.Name.Trim(),
            AccountType = request.AccountType,
            Currency = "CAD",
            OpeningBalance = request.OpeningBalance
        };

        await _repository.AddAsync(account, ct);

        return MapToResponse(account);
    }

    public async Task<FinancialAccountResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateAccountRequest request,
        CancellationToken ct = default)
    {
        var account = await _repository.GetByIdAsync(
            id, userId, ct);

        if (account is null)
        {
            return null;
        }

        ValidateAccount(request.Name, request.AccountType);

        account.Name = request.Name.Trim();
        account.AccountType = request.AccountType;
        account.OpeningBalance = request.OpeningBalance;

        await _repository.UpdateAsync(account, ct);

        return MapToResponse(account);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var account = await _repository.GetByIdAsync(
            id, userId, ct);

        if (account is null)
        {
            return false;
        }

        await _repository.DeleteAsync(account, ct);

        return true;
    }

    private static void ValidateAccount(
        string name,
        string accountType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Account name is required.");
        }

        if (name.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Account name cannot exceed 150 characters.");
        }

        if (!ValidAccountTypes.Contains(accountType))
        {
            throw new ArgumentException(
                "Invalid account type.");
        }
    }

    private static FinancialAccountResponse MapToResponse(
        FinancialAccount account)
    {
        return new FinancialAccountResponse(
            account.Id,
            account.Name,
            account.AccountType,
            account.Currency,
            account.OpeningBalance,
            account.CreatedAtUtc
        );
    }
}
