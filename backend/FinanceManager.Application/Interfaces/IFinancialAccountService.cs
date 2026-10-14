
using FinanceManager.Application.DTOs.Accounts;

namespace FinanceManager.Application.Interfaces;

public interface IFinancialAccountService
{
    Task<List<FinancialAccountResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<FinancialAccountResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<FinancialAccountResponse> CreateAsync(
        Guid userId,
        CreateAccountRequest request,
        CancellationToken ct = default);

    Task<FinancialAccountResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateAccountRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);
}
