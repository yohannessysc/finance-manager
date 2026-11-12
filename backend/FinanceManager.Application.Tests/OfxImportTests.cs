using System.Text;

using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Moq;

namespace FinanceManager.Application.Tests;

public sealed class OfxImportTests : IDisposable
{
    // OFX 1.x: SGML header, leaf tags not closed.
    private const string SgmlStatement = """
        OFXHEADER:100
        DATA:OFXSGML
        VERSION:102

        <OFX>
        <BANKMSGSRSV1><STMTTRNRS><STMTRS>
        <CURDEF>CAD
        <BANKACCTFROM><BANKID>001<ACCTID>12345<ACCTTYPE>CHECKING</BANKACCTFROM>
        <BANKTRANLIST>
        <STMTTRN>
        <TRNTYPE>DEBIT
        <DTPOSTED>20261103120000[-5:EST]
        <TRNAMT>-54.20
        <FITID>90001
        <NAME>LOBLAWS #123
        <MEMO>Groceries
        </STMTTRN>
        <STMTTRN>
        <TRNTYPE>CREDIT
        <DTPOSTED>20261105
        <TRNAMT>2500.00
        <FITID>90002
        <NAME>PAYROLL ACME &amp; CO
        </STMTTRN>
        </BANKTRANLIST>
        </STMTRS></STMTTRNRS></BANKMSGSRSV1>
        </OFX>
        """;

    // OFX 2.x: XML.
    private const string XmlStatement = """
        <?xml version="1.0" encoding="UTF-8"?>
        <?OFX OFXHEADER="200" VERSION="220"?>
        <OFX><CREDITCARDMSGSRSV1><CCSTMTTRNRS><CCSTMTRS>
          <CURDEF>USD</CURDEF>
          <BANKTRANLIST>
            <STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20261101</DTPOSTED><TRNAMT>-12,50</TRNAMT><FITID>A1</FITID><NAME>Coffee</NAME></STMTTRN>
          </BANKTRANLIST>
        </CCSTMTRS></CCSTMTTRNRS></CREDITCARDMSGSRSV1></OFX>
        """;

    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly OfxTransactionImportService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    public OfxImportTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "ofx@example.com",
            Email = "ofx@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.FinancialAccounts.Add(new FinancialAccount
        {
            Id = _accountId,
            UserId = _userId,
            Name = "Chequing",
            Currency = "CAD"
        });
        _db.UserPreferences.Add(new UserPreference { UserId = _userId, TimeZone = "America/Toronto" });
        _db.SaveChanges();

        _service = new OfxTransactionImportService(
            _db,
            new FinancialTransactionRepository(_db),
            new Mock<ICategoryRuleService>().Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static MemoryStream Stream(string content) => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void ParsesSgmlStatements()
    {
        var statement = OfxParser.Parse(SgmlStatement);

        Assert.Equal("CAD", statement.Currency);
        Assert.Equal("12345", statement.AccountId);
        Assert.Equal(2, statement.Transactions.Count);

        var groceries = statement.Transactions[0];
        Assert.Equal("90001", groceries.FitId);
        Assert.Equal(new DateOnly(2026, 11, 3), groceries.Date);
        Assert.Equal(-54.20m, groceries.Amount);
        Assert.Equal("LOBLAWS #123", groceries.Name);
        Assert.Equal("Groceries", groceries.Memo);
        Assert.Equal("PAYROLL ACME & CO", statement.Transactions[1].Name);
    }

    [Fact]
    public void ParsesXmlStatementsWithDecimalCommas()
    {
        var statement = OfxParser.Parse(XmlStatement);

        Assert.Equal("USD", statement.Currency);
        var coffee = Assert.Single(statement.Transactions);
        Assert.Equal(-12.50m, coffee.Amount);
        Assert.Equal("Coffee", coffee.Name);
    }

    [Fact]
    public void RejectsFilesThatAreNotOfx()
    {
        Assert.Throws<FormatException>(() => OfxParser.Parse("date,amount\n2026-01-01,5"));
    }

    [Fact]
    public async Task ImportsOnceAndSkipsTransactionsAlreadyImported()
    {
        var first = await _service.ImportAsync(_userId, _accountId, Stream(SgmlStatement));
        var second = await _service.ImportAsync(_userId, _accountId, Stream(SgmlStatement));

        Assert.Equal(2, first.Imported);
        Assert.Equal(0, second.Imported);
        Assert.Equal(2, second.Duplicates);

        var transactions = await _db.FinancialTransactions.OrderBy(x => x.Amount).ToListAsync();
        Assert.Equal(TransactionType.Expense, transactions[0].Type);
        Assert.Equal(54.20m, transactions[0].Amount);
        Assert.Equal("LOBLAWS #123 — Groceries", transactions[0].Description);
        Assert.Equal(TransactionType.Income, transactions[1].Type);
        // Midnight Toronto time (EST, UTC-5) on the posted date.
        Assert.Equal(new DateTime(2026, 11, 3, 5, 0, 0, DateTimeKind.Utc), transactions[0].TransactionDateUtc);
    }

    [Fact]
    public async Task RejectsAStatementInAnotherCurrency()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ImportAsync(_userId, _accountId, Stream(XmlStatement)));
    }

    [Fact]
    public async Task RejectsAccountsThatSyncFromTheBank()
    {
        var account = await _db.FinancialAccounts.SingleAsync();
        account.BankConnectionId = Guid.NewGuid();
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ImportAsync(_userId, _accountId, Stream(SgmlStatement)));
    }
}
