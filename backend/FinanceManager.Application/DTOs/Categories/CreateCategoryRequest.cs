
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Categories;

public record CreateCategoryRequest(
    string Name,
    TransactionType Type
);
