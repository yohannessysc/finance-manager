using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Billing;

public sealed record BillingStatusResponse(
    SubscriptionPlan Plan,
    SubscriptionPlan? SubscribedPlan,
    string? SubscriptionStatus,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    bool HasBillingAccount,
    bool BillingConfigured);

public sealed record CheckoutRequest(
    SubscriptionPlan Plan,
    BillingInterval Interval = BillingInterval.Monthly);

public sealed record BillingRedirectResponse(string Url);
