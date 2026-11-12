
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

        await ValidateParentAsync(
            userId,
            request.Type,
            request.ParentCategoryId,
            categoryId: null,
            ct);

        var category = new TransactionCategory
        {
            UserId = userId,
            Name = name,
            Type = request.Type,
            ParentCategoryId = request.ParentCategoryId,
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
        if (category.UserId != userId)
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

        await ValidateParentAsync(
            userId,
            category.Type,
            request.ParentCategoryId,
            category.Id,
            ct);

        category.Name = name;
        category.ParentCategoryId = request.ParentCategoryId;

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
        if (category.UserId != userId)
            return false;

        if (await _repository.HasSubcategoriesAsync(id, ct))
        {
            throw new InvalidOperationException(
                "Move or delete this category's subcategories first.");
        }

        if (await _repository.IsInUseAsync(id, userId, ct))
        {
            throw new InvalidOperationException(
                "This category is used by transactions or recurring schedules and cannot be deleted.");
        }

        await _repository.DeleteAsync(category, ct);

        return true;
    }

    private async Task ValidateParentAsync(
        Guid userId,
        TransactionType type,
        Guid? parentId,
        Guid? categoryId,
        CancellationToken ct)
    {
        if (parentId is null)
            return;

        if (parentId == categoryId)
            throw new ArgumentException("A category cannot be its own parent.");

        var parent = await _repository.GetByIdAsync(parentId.Value, userId, ct);

        if (parent is null || parent.UserId != userId)
            throw new ArgumentException("Choose one of your own categories as the parent.");
        if (parent.Type != type)
            throw new ArgumentException("A subcategory must have the same type as its parent.");
        if (parent.ParentCategoryId is not null)
            throw new ArgumentException("Subcategories cannot have subcategories of their own.");
        if (categoryId is not null &&
            await _repository.HasSubcategoriesAsync(categoryId.Value, ct))
        {
            throw new ArgumentException(
                "A category with subcategories cannot become a subcategory.");
        }
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
            category.CreatedAtUtc,
            category.ParentCategoryId
        );
    }
}
