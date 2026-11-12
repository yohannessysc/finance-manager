using FinanceManager.Application.DTOs.Billing;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Billing;

public sealed class BillingService : IBillingService, IEntitlementService
{
    private static readonly HashSet<string> SubscriptionEvents =
    [
        "checkout.session.completed",
        "customer.subscription.created",
        "customer.subscription.updated",
        "customer.subscription.deleted",
        "customer.subscription.paused",
        "customer.subscription.resumed"
    ];

    private readonly FinanceDbContext _db;
    private readonly IStripeGateway _stripe;
    private readonly BillingOptions _options;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        FinanceDbContext db,
        IStripeGateway stripe,
        IOptions<BillingOptions> options,
        IConfiguration configuration,
        ILogger<BillingService> logger)
    {
        _db = db;
        _stripe = stripe;
        _options = options.Value;
        _frontendBaseUrl = (configuration["Frontend:BaseUrl"] ?? "").TrimEnd('/');
        _logger = logger;
    }

    public async Task<SubscriptionPlan> GetPlanAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var subscription = await FindAsync(userId, ct, tracking: false);
        return Max(subscription?.EffectivePlan ?? SubscriptionPlan.Free, _options.DefaultPlan);
    }

    public async Task<BillingStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var subscription = await FindAsync(userId, ct, tracking: false);
        var subscribed = subscription?.StripeSubscriptionId is null
            ? (SubscriptionPlan?)null
            : subscription.Plan;

        return new BillingStatusResponse(
            Max(subscription?.EffectivePlan ?? SubscriptionPlan.Free, _options.DefaultPlan),
            subscribed,
            subscription?.Status,
            subscription?.CurrentPeriodEndUtc,
            subscription?.CancelAtPeriodEnd ?? false,
            subscription is not null,
            _options.IsConfigured);
    }

    public async Task<BillingRedirectResponse> CreateCheckoutSessionAsync(
        Guid userId,
        string email,
        CheckoutRequest request,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        if (request.Plan == SubscriptionPlan.Free || !Enum.IsDefined(request.Plan))
            throw new ArgumentException("Choose a paid plan.");

        var priceId = _options.GetPriceId(request.Plan, request.Interval)
            ?? throw new ArgumentException("That plan is not available for this billing interval.");

        var subscription = await FindAsync(userId, ct, tracking: true);

        if (subscription?.EffectivePlan > SubscriptionPlan.Free)
        {
            throw new InvalidOperationException(
                "You already have a subscription. Use Manage billing to change plans.");
        }

        // One Stripe customer per user, created before checkout so every
        // webhook can be matched back to the user by customer id.
        if (subscription is null)
        {
            subscription = new UserSubscription
            {
                UserId = userId,
                StripeCustomerId = await _stripe.CreateCustomerAsync(userId, email, ct)
            };
            _db.UserSubscriptions.Add(subscription);
            await _db.SaveChangesAsync(ct);
        }

        var url = await _stripe.CreateCheckoutSessionAsync(
            subscription.StripeCustomerId,
            userId,
            priceId,
            _options.TrialDays,
            $"{_frontendBaseUrl}/billing?checkout=success",
            $"{_frontendBaseUrl}/billing?checkout=cancelled",
            ct);

        return new BillingRedirectResponse(url);
    }

    public async Task<BillingRedirectResponse> CreatePortalSessionAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        var subscription = await FindAsync(userId, ct, tracking: false)
            ?? throw new ArgumentException("You do not have a billing account yet.");

        var url = await _stripe.CreatePortalSessionAsync(
            subscription.StripeCustomerId,
            $"{_frontendBaseUrl}/billing",
            ct);

        return new BillingRedirectResponse(url);
    }

    public async Task CancelSubscriptionAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var subscription = await FindAsync(userId, ct, tracking: false);

        if (subscription?.StripeSubscriptionId is null ||
            subscription.EffectivePlan == SubscriptionPlan.Free)
        {
            return;
        }

        EnsureConfigured();
        await _stripe.CancelSubscriptionAsync(subscription.StripeSubscriptionId, ct);
    }

    public async Task HandleWebhookAsync(
        string payload,
        string signature,
        CancellationToken ct = default)
    {
        EnsureConfigured();

        var stripeEvent = _stripe.ParseWebhook(payload, signature);

        if (!SubscriptionEvents.Contains(stripeEvent.Type) ||
            stripeEvent.SubscriptionId is null)
        {
            return;
        }

        // Events can arrive out of order or be retried, so apply the
        // subscription's current state rather than the event payload.
        var snapshot = await _stripe.GetSubscriptionAsync(stripeEvent.SubscriptionId, ct);

        var subscription = await _db.UserSubscriptions
            .SingleOrDefaultAsync(x => x.StripeCustomerId == snapshot.CustomerId, ct);

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe subscription {SubscriptionId} belongs to unknown customer {CustomerId}.",
                snapshot.Id,
                snapshot.CustomerId);
            return;
        }

        var entitled = UserSubscription.EntitledStatuses.Contains(snapshot.Status);

        // An old, ended subscription must not overwrite the current one.
        if (subscription.StripeSubscriptionId is not null &&
            subscription.StripeSubscriptionId != snapshot.Id &&
            !entitled)
        {
            return;
        }

        var plan = _options.GetPlanForPrice(snapshot.PriceId);
        if (plan is null)
        {
            _logger.LogError(
                "Stripe price {PriceId} is not mapped to a plan; subscription {SubscriptionId} ignored.",
                snapshot.PriceId,
                snapshot.Id);
            return;
        }

        subscription.StripeSubscriptionId = snapshot.Id;
        subscription.Plan = plan.Value;
        subscription.Status = snapshot.Status;
        subscription.CurrentPeriodEndUtc = snapshot.CurrentPeriodEndUtc;
        subscription.CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd;
        subscription.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} subscription is {Status} on plan {Plan}.",
            subscription.UserId,
            subscription.Status,
            subscription.Plan);
    }

    private Task<UserSubscription?> FindAsync(
        Guid userId,
        CancellationToken ct,
        bool tracking)
    {
        var query = tracking
            ? _db.UserSubscriptions
            : _db.UserSubscriptions.AsNoTracking();

        return query.SingleOrDefaultAsync(x => x.UserId == userId, ct);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new BillingNotConfiguredException();
    }

    private static SubscriptionPlan Max(SubscriptionPlan left, SubscriptionPlan right) =>
        left > right ? left : right;
}
