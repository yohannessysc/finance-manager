
using FinanceManager.Application.DTOs.Accounts;

namespace FinanceManager.Application.Interfaces;

public interface IAccountBalanceService
{
    Task<List<AccountBalanceResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<AccountBalanceResponse?> GetByAccountIdAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default);
}
