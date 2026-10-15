
using FinanceManager.Application.DTOs.Categories;

namespace FinanceManager.Application.Interfaces;

public interface ITransactionCategoryService
{
    Task<List<CategoryResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<CategoryResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<CategoryResponse> CreateAsync(
        Guid userId,
        CreateCategoryRequest request,
        CancellationToken ct = default);

    Task<CategoryResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateCategoryRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);
}
