
using FinanceManager.Application.DTOs.Categories;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Services;

public class TransactionCategoryService : ITransactionCategoryService
{
    private readonly ITransactionCategoryRepository _repository;

    public TransactionCategoryService(
        ITransactionCategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CategoryResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var categories = await _repository.GetAllAsync(userId, ct);

        return categories.Select(MapToResponse).ToList();
    }

    public async Task<CategoryResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(id, userId, ct);

        return category is null
            ? null
            : MapToResponse(category);
    }

    public async Task<CategoryResponse> CreateAsync(
        Guid userId,
        CreateCategoryRequest request,
        CancellationToken ct = default)
    {
        ValidateName(request.Name);
        ValidateType(request.Type);

        var name = request.Name.Trim();

        if (await _repository.NameExistsAsync(
            userId, request.Type, name, ct: ct))
        {
            throw new ArgumentException(
                "A category with this name already exists for this type.");
        }

        var category = new TransactionCategory
        {
            UserId = userId,
            Name = name,
            Type = request.Type,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddAsync(category, ct);

        return MapToResponse(category);
    }

    public async Task<CategoryResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateCategoryRequest request,
        CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(
            id, userId, ct);

        if (category is null)
            return null;

        ValidateName(request.Name);

        var name = request.Name.Trim();

        if (await _repository.NameExistsAsync(
            userId,
            category.Type,
            name,
            excludeId: id,
            ct: ct))
        {
            throw new ArgumentException(
                "A category with this name already exists for this type.");
        }

        category.Name = name;

        await _repository.SaveChangesAsync(ct);

        return MapToResponse(category);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(
            id, userId, ct);

        if (category is null)
            return false;

        if (await _repository.IsInUseAsync(id, userId, ct))
        {
            throw new InvalidOperationException(
                "This category is used by transactions or recurring schedules and cannot be deleted.");
        }

        await _repository.DeleteAsync(category, ct);

        return true;
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Category name is required.");
        }

        if (name.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Category name cannot exceed 150 characters.");
        }
    }

    private static void ValidateType(TransactionType type)
    {
        if (type != TransactionType.Income &&
            type != TransactionType.Expense)
        {
            throw new ArgumentException(
                "Categories can only be Income or Expense.");
        }
    }

    private static CategoryResponse MapToResponse(
        TransactionCategory category)
    {
        return new CategoryResponse(
            category.Id,
            category.Name,
            category.Type,
            category.CreatedAtUtc
        );
    }
}
