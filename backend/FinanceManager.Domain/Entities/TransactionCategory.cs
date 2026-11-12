
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

public class TransactionCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public TransactionType Type { get; set; }

    // Set for a subcategory (e.g. Groceries under Food). One level deep:
    // a parent cannot itself have a parent. Same owner and type.
    public Guid? ParentCategoryId { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
