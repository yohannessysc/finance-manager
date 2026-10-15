
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Services;

public class DefaultCategorySeeder
{
    private readonly ITransactionCategoryRepository _repository;

    private static readonly string[] ExpenseCategories =
    {
        "Rent & Housing",
        "Groceries",
        "Transportation",
        "Car Insurance",
        "Utilities",
        "Subscriptions",
        "Restaurants",
        "Entertainment",
        "Health",
        "Shopping",
        "Education",
        "Other Expenses"
    };

    private static readonly string[] IncomeCategories =
    {
        "Salary",
        "Freelance Income",
        "Business Income",
        "Rideshare Income",
        "Investment Income",
        "Gifts",
        "Other Income"
    };

    public DefaultCategorySeeder(
        ITransactionCategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task SeedForUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetAllAsync(userId, ct);

        foreach (var name in ExpenseCategories)
        {
            if (existing.Any(x =>
                x.Type == TransactionType.Expense &&
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            await _repository.AddAsync(
                new TransactionCategory
                {
                    UserId = userId,
                    Name = name,
                    Type = TransactionType.Expense,
                    CreatedAtUtc = DateTime.UtcNow
                },
                ct);
        }

        foreach (var name in IncomeCategories)
        {
            if (existing.Any(x =>
                x.Type == TransactionType.Income &&
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            await _repository.AddAsync(
                new TransactionCategory
                {
                    UserId = userId,
                    Name = name,
                    Type = TransactionType.Income,
                    CreatedAtUtc = DateTime.UtcNow
                },
                ct);
        }
    }
}
