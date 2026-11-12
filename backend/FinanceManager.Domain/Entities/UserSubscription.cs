using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

// Local mirror of the user's Stripe subscription. Stripe is the
// source of truth; webhooks keep this row current.
public sealed class UserSubscription
{
    // Stripe statuses that still grant the paid plan. past_due keeps
    // access while Stripe retries the payment.
    public static readonly string[] EntitledStatuses =
        ["active", "trialing", "past_due"];

    public Guid UserId { get; set; }

    public string StripeCustomerId { get; set; } = string.Empty;

    public string? StripeSubscriptionId { get; set; }

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

    public string? Status { get; set; }

    public DateTime? CurrentPeriodEndUtc { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public SubscriptionPlan EffectivePlan =>
        Status is not null && EntitledStatuses.Contains(Status)
            ? Plan
            : SubscriptionPlan.Free;
}
