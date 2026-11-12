using FinanceManager.Application.DTOs.Categories;
using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class SplitsAndSubcategoriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly FinancialTransactionService _transactions;
    private readonly TransactionCategoryService _categories;
    private readonly BudgetService _budgets;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    public SplitsAndSubcategoriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "split@example.com",
            Email = "split@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.FinancialAccounts.Add(new FinancialAccount
        {
            Id = _accountId,
            UserId = _userId,
            Name = "Chequing",
            Currency = "CAD"
        });
        _db.UserPreferences.Add(new UserPreference
        {
            UserId = _userId,
            BudgetStartDay = 1,
            TimeZone = "UTC"
        });
        _db.SaveChanges();

        _transactions = new FinancialTransactionService(new FinancialTransactionRepository(_db));
        _categories = new TransactionCategoryService(new TransactionCategoryRepository(_db));
        _budgets = new BudgetService(_db, new HouseholdAuthorizationService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Guid> CategoryAsync(string name, Guid? parentId = null) =>
        (await _categories.CreateAsync(
            _userId,
            new CreateCategoryRequest(name, TransactionType.Expense, parentId))).Id;

    private async Task<TransactionResponse> ExpenseAsync(decimal amount, Guid? categoryId) =>
        await _transactions.CreateAsync(
            _userId,
            new CreateTransactionRequest(
                _accountId,
                null,
                categoryId,
                TransactionType.Expense,
                amount,
                "Costco",
                null,
                DateTime.UtcNow.AddMinutes(-5)));

    [Fact]
    public async Task SplitLinesMustAddUpToTheAmount()
    {
        var groceries = await CategoryAsync("Groceries");
        var household = await CategoryAsync("Household");
        var expense = await ExpenseAsync(100m, groceries);

        await Assert.ThrowsAsync<ArgumentException>(() => _transactions.SetSplitsAsync(
            expense.Id,
            _userId,
            new SetSplitsRequest([new(groceries, 60m, null), new(household, 30m, null)])));
    }

    [Fact]
    public async Task SplitReplacesTheCategoryAndBudgetsCountEachLine()
    {
        var groceries = await CategoryAsync("Groceries");
        var household = await CategoryAsync("Household");
        var expense = await ExpenseAsync(100m, groceries);

        var split = await _transactions.SetSplitsAsync(
            expense.Id,
            _userId,
            new SetSplitsRequest([new(groceries, 70m, "Food"), new(household, 30m, null)]));

        Assert.NotNull(split);
        Assert.Null(split!.CategoryId);
        Assert.Equal(2, split.Splits!.Count);

        _db.MonthlyBudgets.Add(new MonthlyBudget { UserId = _userId, CategoryId = household, MonthlyLimit = 50 });
        await _db.SaveChangesAsync();

        var budget = Assert.Single(await _budgets.GetAllAsync(_userId));
        Assert.Equal(30m, budget.AmountSpent);
    }

    [Fact]
    public async Task SplitTransactionAmountCannotChangeUntilTheSplitIsRemoved()
    {
        var groceries = await CategoryAsync("Groceries");
        var household = await CategoryAsync("Household");
        var expense = await ExpenseAsync(100m, groceries);
        await _transactions.SetSplitsAsync(
            expense.Id,
            _userId,
            new SetSplitsRequest([new(groceries, 70m, null), new(household, 30m, null)]));

        UpdateTransactionRequest Update(decimal amount, Guid? categoryId) => new(
            _accountId, null, categoryId, TransactionType.Expense, amount,
            "Costco", null, expense.TransactionDateUtc);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _transactions.UpdateAsync(expense.Id, _userId, Update(120m, null)));

        // Picking one category instead removes the split.
        var updated = await _transactions.UpdateAsync(expense.Id, _userId, Update(120m, groceries));
        Assert.Null(updated!.Splits);
        Assert.Equal(groceries, updated.CategoryId);
        Assert.Empty(_db.TransactionSplits);
    }

    [Fact]
    public async Task ParentBudgetIncludesItsSubcategories()
    {
        var food = await CategoryAsync("Food");
        var groceries = await CategoryAsync("Groceries", food);
        await ExpenseAsync(40m, groceries);
        await ExpenseAsync(10m, food);

        _db.MonthlyBudgets.Add(new MonthlyBudget { UserId = _userId, CategoryId = food, MonthlyLimit = 100 });
        _db.MonthlyBudgets.Add(new MonthlyBudget { UserId = _userId, CategoryId = groceries, MonthlyLimit = 100 });
        await _db.SaveChangesAsync();

        var budgets = await _budgets.GetAllAsync(_userId);
        Assert.Equal(50m, budgets.Single(x => x.CategoryId == food).AmountSpent);
        Assert.Equal(40m, budgets.Single(x => x.CategoryId == groceries).AmountSpent);
    }

    [Fact]
    public async Task SubcategoriesAreOneLevelDeep()
    {
        var food = await CategoryAsync("Food");
        var groceries = await CategoryAsync("Groceries", food);

        await Assert.ThrowsAsync<ArgumentException>(() => CategoryAsync("Costco", groceries));

        // A parent with subcategories cannot itself be moved under another.
        var other = await CategoryAsync("Shopping");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _categories.UpdateAsync(food, _userId, new UpdateCategoryRequest("Food", other)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _categories.DeleteAsync(food, _userId));
    }

    [Fact]
    public async Task SubcategoryMustMatchItsParentsType()
    {
        var salary = (await _categories.CreateAsync(
            _userId,
            new CreateCategoryRequest("Salary", TransactionType.Income))).Id;

        await Assert.ThrowsAsync<ArgumentException>(() => CategoryAsync("Bonus", salary));
    }
}

public sealed class BulkCategoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly FinancialTransactionService _transactions;
    private readonly TransactionCategoryService _categories;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _otherAccountId = Guid.NewGuid();

    public BulkCategoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "bulk@example.com",
            Email = "bulk@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.FinancialAccounts.AddRange(
            new FinancialAccount { Id = _accountId, UserId = _userId, Name = "Chequing", Currency = "CAD" },
            new FinancialAccount { Id = _otherAccountId, UserId = _userId, Name = "Savings", Currency = "CAD" });
        _db.SaveChanges();

        _transactions = new FinancialTransactionService(new FinancialTransactionRepository(_db));
        _categories = new TransactionCategoryService(new TransactionCategoryRepository(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Guid> AddAsync(TransactionType type, Guid? destination = null) =>
        (await _transactions.CreateAsync(_userId, new CreateTransactionRequest(
            _accountId, destination, null, type, 10m, "x", null, DateTime.UtcNow.AddMinutes(-1)))).Id;

    [Fact]
    public async Task SetsTheCategoryOnMatchingTransactionsAndSkipsTheRest()
    {
        var dining = (await _categories.CreateAsync(
            _userId, new CreateCategoryRequest("Dining", TransactionType.Expense))).Id;
        var groceries = (await _categories.CreateAsync(
            _userId, new CreateCategoryRequest("Groceries", TransactionType.Expense))).Id;

        var lunch = await AddAsync(TransactionType.Expense);
        var dinner = await AddAsync(TransactionType.Expense);
        var pay = await AddAsync(TransactionType.Income);
        var transfer = await AddAsync(TransactionType.Transfer, _otherAccountId);

        // A split expense gets the single category instead.
        await _transactions.UpdateAsync(dinner, _userId, new UpdateTransactionRequest(
            _accountId, null, groceries, TransactionType.Expense, 10m, "x", null, DateTime.UtcNow.AddMinutes(-1)));
        await _transactions.SetSplitsAsync(dinner, _userId, new SetSplitsRequest(
            [new(groceries, 4m, null), new(dining, 6m, null)]));

        var result = await _transactions.SetCategoryAsync(
            _userId,
            new BulkCategoryRequest([lunch, dinner, pay, transfer, Guid.NewGuid()], dining));

        Assert.Equal(2, result.Updated);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(dining, (await _transactions.GetByIdAsync(lunch, _userId))!.CategoryId);
        var updatedDinner = await _transactions.GetByIdAsync(dinner, _userId);
        Assert.Equal(dining, updatedDinner!.CategoryId);
        Assert.Null(updatedDinner.Splits);
        Assert.Null((await _transactions.GetByIdAsync(pay, _userId))!.CategoryId);
    }

    [Fact]
    public async Task RejectsACategoryTheUserCannotUse()
    {
        var id = await AddAsync(TransactionType.Expense);

        await Assert.ThrowsAsync<ArgumentException>(() => _transactions.SetCategoryAsync(
            _userId, new BulkCategoryRequest([id], Guid.NewGuid())));
    }
}
