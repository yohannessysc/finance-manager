using FinanceManager.Application.Interfaces;

using Microsoft.Extensions.Options;

using Stripe;

namespace FinanceManager.Infrastructure.Billing;

public sealed record StripeSubscriptionSnapshot(
    string Id,
    string CustomerId,
    string Status,
    string? PriceId,
    DateTime? CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

public sealed record StripeWebhookEvent(string Type, string? SubscriptionId);

// The only code that talks to Stripe, so billing logic is testable.
public interface IStripeGateway
{
    Task<string> CreateCustomerAsync(Guid userId, string email, CancellationToken ct);

    Task<string> CreateCheckoutSessionAsync(
        string customerId,
        Guid userId,
        string priceId,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct);

    Task<string> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken ct);

    Task<StripeSubscriptionSnapshot> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken ct);

    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);

    StripeWebhookEvent ParseWebhook(string payload, string signature);
}

public sealed class StripeGateway : IStripeGateway
{
    private readonly BillingOptions _options;
    private readonly Lazy<StripeClient> _client;

    public StripeGateway(IOptions<BillingOptions> options)
    {
        _options = options.Value;
        _client = new Lazy<StripeClient>(() =>
            new StripeClient(_options.StripeSecretKey));
    }

    public async Task<string> CreateCustomerAsync(
        Guid userId,
        string email,
        CancellationToken ct)
    {
        var customer = await new CustomerService(_client.Value).CreateAsync(
            new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId.ToString()
                }
            },
            cancellationToken: ct);

        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        string customerId,
        Guid userId,
        string priceId,
        int trialDays,
        string successUrl,
        string cancelUrl,
        CancellationToken ct)
    {
        var session = await new Stripe.Checkout.SessionService(_client.Value).CreateAsync(
            new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                Customer = customerId,
                ClientReferenceId = userId.ToString(),
                LineItems =
                [
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                ],
                SubscriptionData = trialDays > 0
                    ? new Stripe.Checkout.SessionSubscriptionDataOptions
                    {
                        TrialPeriodDays = trialDays
                    }
                    : null,
                AllowPromotionCodes = true,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            },
            cancellationToken: ct);

        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken ct)
    {
        var session = await new Stripe.BillingPortal.SessionService(_client.Value).CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = returnUrl
            },
            cancellationToken: ct);

        return session.Url;
    }

    public async Task<StripeSubscriptionSnapshot> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken ct)
    {
        var subscription = await new SubscriptionService(_client.Value)
            .GetAsync(subscriptionId, cancellationToken: ct);
        var item = subscription.Items?.Data?.FirstOrDefault();

        return new StripeSubscriptionSnapshot(
            subscription.Id,
            subscription.CustomerId,
            subscription.Status,
            item?.Price?.Id,
            item?.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd);
    }

    public async Task CancelSubscriptionAsync(
        string subscriptionId,
        CancellationToken ct)
    {
        await new SubscriptionService(_client.Value)
            .CancelAsync(subscriptionId, cancellationToken: ct);
    }

    public StripeWebhookEvent ParseWebhook(string payload, string signature)
    {
        Event stripeEvent;
        try
        {
            // The subscription is re-fetched from the API afterwards, so a
            // payload from a different API version is still usable.
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                _options.StripeWebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            throw new BillingWebhookException("Invalid Stripe webhook signature.", ex);
        }

        var subscriptionId = stripeEvent.Data.Object switch
        {
            Stripe.Checkout.Session session => session.SubscriptionId,
            Subscription subscription => subscription.Id,
            _ => null
        };

        return new StripeWebhookEvent(stripeEvent.Type, subscriptionId);
    }
}
