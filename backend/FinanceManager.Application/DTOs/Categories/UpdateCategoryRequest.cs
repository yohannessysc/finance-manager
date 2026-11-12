namespace FinanceManager.Application.DTOs.Categories;

// ParentCategoryId null makes it a top-level category.
public record UpdateCategoryRequest(
    string Name,
    Guid? ParentCategoryId = null
);
