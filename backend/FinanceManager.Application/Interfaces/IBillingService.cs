using FinanceManager.Application.DTOs.Billing;

namespace FinanceManager.Application.Interfaces;

public interface IBillingService
{
    Task<BillingStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken ct = default);

    // Throws InvalidOperationException when the user already has a
    // paid subscription (plan changes go through the portal).
    Task<BillingRedirectResponse> CreateCheckoutSessionAsync(
        Guid userId,
        string email,
        CheckoutRequest request,
        CancellationToken ct = default);

    Task<BillingRedirectResponse> CreatePortalSessionAsync(
        Guid userId,
        CancellationToken ct = default);

    // Ends any paid subscription immediately (used on account deletion).
    Task CancelSubscriptionAsync(
        Guid userId,
        CancellationToken ct = default);

    // Verifies the Stripe signature, then syncs the subscription.
    // Throws BillingWebhookException for invalid signatures.
    Task HandleWebhookAsync(
        string payload,
        string signature,
        CancellationToken ct = default);
}

public sealed class BillingWebhookException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class BillingNotConfiguredException()
    : Exception("Billing is not configured.");
