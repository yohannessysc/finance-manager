
using FinanceManager.Application.DTOs.Accounts;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Services;

public class FinancialAccountService : IFinancialAccountService
{
    private readonly IFinancialAccountRepository _repository;
    private readonly IHouseholdAuthorizationService _householdAuthorization;

    private static readonly HashSet<string> ValidAccountTypes =
        new(StringComparer.Ordinal)
        {
            "Chequing",
            "Savings",
            "Cash",
            "CreditCard"
        };

    public FinancialAccountService(
        IFinancialAccountRepository repository,
        IHouseholdAuthorizationService householdAuthorization)
    {
        _repository = repository;
        _householdAuthorization = householdAuthorization;
    }

    public async Task<List<FinancialAccountResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var accounts = await _repository.GetAllAsync(userId, ct);

        return accounts.Select(account =>
            MapToResponse(account, userId)).ToList();
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
            : MapToResponse(account, userId);
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

        return MapToResponse(account, userId);
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

        if (account.UserId != userId)
            return null;

        ValidateAccount(request.Name, request.AccountType);

        account.Name = request.Name.Trim();
        account.AccountType = request.AccountType;
        account.OpeningBalance = request.OpeningBalance;

        await _repository.UpdateAsync(account, ct);

        return MapToResponse(account, userId);
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

        if (account.UserId != userId)
            return false;

        await _repository.DeleteAsync(account, ct);

        return true;
    }

    public async Task<FinancialAccountResponse?> ShareAsync(
        Guid id,
        Guid userId,
        Guid householdId,
        CancellationToken ct = default)
    {
        var account = await _repository.GetByIdAsync(id, userId, ct);
        if (account is null || account.UserId != userId)
            return null;

        if (!await _householdAuthorization.IsMemberAsync(
                userId,
                householdId,
                ct))
        {
            throw new UnauthorizedAccessException(
                "Only a member of the household can share an account there.");
        }

        if (account.HouseholdId is not null &&
            account.HouseholdId != householdId)
        {
            throw new ArgumentException(
                "An account can be shared with only one household.");
        }

        account.HouseholdId = householdId;
        await _repository.UpdateAsync(account, ct);
        return MapToResponse(account, userId);
    }

    public async Task<FinancialAccountResponse?> UnshareAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var account = await _repository.GetByIdAsync(id, userId, ct);
        if (account is null || account.UserId != userId)
            return null;

        account.HouseholdId = null;
        await _repository.UpdateAsync(account, ct);
        return MapToResponse(account, userId);
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
        FinancialAccount account,
        Guid currentUserId)
    {
        return new FinancialAccountResponse(
            account.Id,
            account.Name,
            account.AccountType,
            account.Currency,
            account.OpeningBalance,
            account.CreatedAtUtc,
            account.HouseholdId,
            account.UserId == currentUserId,
            account.BankConnectionId is not null
        );
    }
}
