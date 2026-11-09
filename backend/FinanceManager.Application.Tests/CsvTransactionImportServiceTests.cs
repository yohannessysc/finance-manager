using System.Text;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Application.Tests;

public sealed class CsvTransactionImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly CsvTransactionImportService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FinancialAccount _account;
    private readonly TransactionCategory _category;

    public CsvTransactionImportServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FinanceDbContext(options);
        _db.Database.EnsureCreated();
        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "import@example.com",
            NormalizedUserName = "IMPORT@EXAMPLE.COM",
            Email = "import@example.com",
            NormalizedEmail = "IMPORT@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _account = new FinancialAccount
        {
            UserId = _userId,
            Name = "Checking",
            Currency = "CAD"
        };
        _category = new TransactionCategory
        {
            UserId = _userId,
            Name = "Food",
            Type = TransactionType.Expense
        };
        _db.FinancialAccounts.Add(_account);
        _db.TransactionCategories.Add(_category);
        _db.UserPreferences.Add(new UserPreference
        {
            UserId = _userId,
            TimeZone = "UTC"
        });
        _db.SaveChanges();
        _service = new CsvTransactionImportService(
            _db,
            new FinancialTransactionRepository(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Import_DetectsExistingAndRepeatedRowsAndIsIdempotent()
    {
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            UserId = _userId,
            FinancialAccountId = _account.Id,
            CategoryId = _category.Id,
            Type = TransactionType.Expense,
            Amount = 12.50m,
            Description = "Cafe, lunch",
            Merchant = "Cafe",
            TransactionDateUtc = new DateTime(
                2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync();

        const string csv =
            "date,description,amount,account,currency,type,category,merchant\n" +
            "2026-11-07,\"Cafe, lunch\",12.50,Checking,CAD,Expense,Food,Cafe\n" +
            "2026-11-07,\"Cafe, lunch\",12.50,Checking,CAD,Expense,Food,Cafe\n" +
            "2026-11-08,Market,-25.00,Checking,CAD,,Food,Market\n";

        var firstImport = await Import(csv);
        Assert.Equal(1, firstImport.Imported);
        Assert.Equal(2, firstImport.Duplicates);
        Assert.Equal(0, firstImport.InvalidRows);

        var repeatedImport = await Import(csv);
        Assert.Equal(0, repeatedImport.Imported);
        Assert.Equal(3, repeatedImport.Duplicates);

        var transactions = await _db.FinancialTransactions
            .Where(x => x.UserId == _userId)
            .ToListAsync();
        Assert.Equal(2, transactions.Count);
        Assert.Contains(
            transactions,
            x => x.Description == "Market" &&
                 x.Type == TransactionType.Expense &&
                 x.Amount == 25m);
    }

    [Fact]
    public async Task Import_ReturnsRowErrorsWithoutImportingInvalidRows()
    {
        const string csv =
            "date,description,amount,account\n" +
            "not-a-date,Missing account,10,Unknown\n";

        var result = await Import(csv);

        Assert.Equal(0, result.Imported);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(1, result.InvalidRows);
        Assert.Equal(2, Assert.Single(result.Errors).Row);
    }

    [Fact]
    public async Task Import_RejectsMalformedQuotedCsv()
    {
        const string csv =
            "date,description,amount,account\n" +
            "2026-11-08,\"unterminated,10,Checking\n";

        await Assert.ThrowsAsync<FormatException>(() => Import(csv));
    }

    private async Task<FinanceManager.Application.DTOs.Transactions.CsvTransactionImportResponse>
        Import(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await _service.ImportAsync(_userId, stream);
    }
}
