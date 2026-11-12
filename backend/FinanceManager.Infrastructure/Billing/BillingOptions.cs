using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Billing;

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    // Plan for users without a paid subscription. Development config may
    // raise it so paid features work without Stripe; startup rejects
    // anything but Free outside development.
    public SubscriptionPlan DefaultPlan { get; set; } = SubscriptionPlan.Free;

    public string? StripeSecretKey { get; set; }

    public string? StripeWebhookSecret { get; set; }

    public int TrialDays { get; set; }

    public PlanPrices Bank { get; set; } = new();

    public PlanPrices Premium { get; set; } = new();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(StripeSecretKey) &&
        !string.IsNullOrWhiteSpace(StripeWebhookSecret);

    public string? GetPriceId(SubscriptionPlan plan, BillingInterval interval)
    {
        var prices = plan switch
        {
            SubscriptionPlan.Bank => Bank,
            SubscriptionPlan.Premium => Premium,
            _ => null
        };

        var priceId = interval == BillingInterval.Yearly
            ? prices?.YearlyPriceId
            : prices?.MonthlyPriceId;

        return string.IsNullOrWhiteSpace(priceId) ? null : priceId;
    }

    public SubscriptionPlan? GetPlanForPrice(string? priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId))
            return null;

        if (Bank.Contains(priceId))
            return SubscriptionPlan.Bank;

        return Premium.Contains(priceId) ? SubscriptionPlan.Premium : null;
    }

    public sealed class PlanPrices
    {
        public string? MonthlyPriceId { get; set; }

        public string? YearlyPriceId { get; set; }

        public bool Contains(string priceId) =>
            priceId == MonthlyPriceId || priceId == YearlyPriceId;
    }
}
