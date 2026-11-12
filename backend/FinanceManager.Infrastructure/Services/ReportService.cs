using FinanceManager.Application.DTOs.Reports;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class ReportService : IReportService
{
    public const int MaximumMonths = 36;
    private const string DefaultTimeZoneId = "America/Toronto";

    private readonly FinanceDbContext _db;
    private readonly IHouseholdAuthorizationService _households;

    public ReportService(FinanceDbContext db, IHouseholdAuthorizationService households)
    {
        _db = db;
        _households = households;
    }

    public async Task<CategoryReportResponse> GetCategoryReportAsync(
        Guid userId,
        DateOnly fromMonth,
        DateOnly toMonth,
        string? currency,
        CancellationToken ct = default)
    {
        fromMonth = new DateOnly(fromMonth.Year, fromMonth.Month, 1);
        toMonth = new DateOnly(toMonth.Year, toMonth.Month, 1);
        if (toMonth < fromMonth)
            throw new ArgumentException("The end month must not be before the start month.");

        var months = new List<DateOnly>();
        for (var month = fromMonth; month <= toMonth; month = month.AddMonths(1))
        {
            months.Add(month);
            if (months.Count > MaximumMonths)
                throw new ArgumentException($"Reports can cover at most {MaximumMonths} months.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            await _db.UserPreferences
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.TimeZone)
                .FirstOrDefaultAsync(ct) ?? DefaultTimeZoneId);

        var startUtc = ToUtc(fromMonth, timeZone);
        var endUtc = ToUtc(toMonth.AddMonths(1), timeZone);
        var nowUtc = DateTime.UtcNow;

        // Same scope as the transaction list: own accounts and the
        // households the user belongs to.
        var householdIds = await _households.GetHouseholdIdsAsync(userId, ct);
        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x => x.UserId == userId ||
                        (x.HouseholdId.HasValue && householdIds.Contains(x.HouseholdId.Value)))
            .Select(x => new { x.Id, x.Currency })
            .ToDictionaryAsync(x => x.Id, x => x.Currency.ToUpperInvariant(), ct);
        var accountIds = accounts.Keys.ToList();

        var lines = await CategoryLineReader.ReadAsync(
            _db,
            _db.FinancialTransactions.AsNoTracking().Where(t =>
                accountIds.Contains(t.FinancialAccountId) &&
                t.Type != TransactionType.Transfer &&
                t.TransactionDateUtc >= startUtc &&
                t.TransactionDateUtc < endUtc &&
                t.TransactionDateUtc <= nowUtc),
            ct);

        var currencies = lines
            .GroupBy(line => accounts[line.AccountId])
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

        var selected = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(selected))
            selected = currencies.FirstOrDefault() ?? accounts.Values.FirstOrDefault() ?? "CAD";

        lines = lines.Where(line => accounts[line.AccountId] == selected).ToList();

        var monthIndex = months
            .Select((month, index) => (month, index))
            .ToDictionary(x => x.month, x => x.index);
        int IndexOf(DateTime dateUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(dateUtc, timeZone);
            return monthIndex[new DateOnly(local.Year, local.Month, 1)];
        }

        var categories = await CategoryLineReader.ReadCategoriesAsync(
            _db,
            lines.Select(line => line.CategoryId),
            ct);

        decimal[] Series(IEnumerable<CategoryLine> items)
        {
            var series = new decimal[months.Count];
            foreach (var item in items)
                series[IndexOf(item.DateUtc)] += item.Amount;
            return series;
        }

        Guid? TopLevel(Guid? id) =>
            id is { } value && categories.TryGetValue(value, out var info) && info.ParentId is { } parent
                ? parent
                : id;

        string NameOf(Guid? id) =>
            id is { } value && categories.TryGetValue(value, out var info) ? info.Name : "Uncategorized";

        var rows = lines
            .GroupBy(line => (line.Type, Parent: TopLevel(line.CategoryId)))
            .Select(group =>
            {
                var children = new List<CategoryReportRow>();
                var bySubcategory = group
                    .Where(line => line.CategoryId != group.Key.Parent)
                    .GroupBy(line => line.CategoryId)
                    .ToList();

                if (bySubcategory.Count > 0)
                {
                    children.AddRange(bySubcategory.Select(sub =>
                    {
                        var series = Series(sub);
                        return new CategoryReportRow(sub.Key, NameOf(sub.Key), group.Key.Type, series.Sum(), series, []);
                    }));

                    var direct = group.Where(line => line.CategoryId == group.Key.Parent).ToList();
                    if (direct.Count > 0)
                    {
                        var series = Series(direct);
                        children.Add(new CategoryReportRow(group.Key.Parent, "General", group.Key.Type, series.Sum(), series, []));
                    }
                }

                var total = Series(group);
                return new CategoryReportRow(
                    group.Key.Parent,
                    NameOf(group.Key.Parent),
                    group.Key.Type,
                    total.Sum(),
                    total,
                    children.OrderByDescending(x => x.Total).ToList());
            })
            .OrderBy(row => row.Type)
            .ThenByDescending(row => row.Total)
            .ToList();

        return new CategoryReportResponse(
            selected,
            currencies,
            months.Select(month => month.ToString("yyyy-MM")).ToList(),
            Series(lines.Where(line => line.Type == TransactionType.Income)),
            Series(lines.Where(line => line.Type == TransactionType.Expense)),
            rows);
    }

    private static DateTime ToUtc(DateOnly month, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(month.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
            timeZone);
}
