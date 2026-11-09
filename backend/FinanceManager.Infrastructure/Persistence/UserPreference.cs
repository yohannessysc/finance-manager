namespace FinanceManager.Infrastructure.Persistence;

public sealed class UserPreference
{
    public Guid UserId { get; set; }

    public string DefaultCurrency { get; set; } = "CAD";

    public string TimeZone { get; set; } = "America/Toronto";

    public string DateFormat { get; set; } = "yyyy-MM-dd";

    public int BudgetStartDay { get; set; } = 1;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
