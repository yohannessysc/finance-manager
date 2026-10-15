
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Categories;

public record CategoryResponse(
    Guid Id,
    string Name,
    TransactionType Type,
    DateTime CreatedAtUtc
);
