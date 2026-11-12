using System.Text.Json;

using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services.Plaid;

// Applies a verified Plaid webhook: queues a sync when new transactions
// are ready and keeps the connection status in step with the item.
// https://plaid.com/docs/api/items/#webhooks
public sealed class PlaidWebhookHandler
{
    private readonly FinanceDbContext _db;
    private readonly IBankSyncQueue _syncQueue;
    private readonly IEntitlementService _entitlements;
    private readonly ILogger<PlaidWebhookHandler> _logger;

    public PlaidWebhookHandler(
        FinanceDbContext db,
        IBankSyncQueue syncQueue,
        IEntitlementService entitlements,
        ILogger<PlaidWebhookHandler> logger)
    {
        _db = db;
        _syncQueue = syncQueue;
        _entitlements = entitlements;
        _logger = logger;
    }

    public async Task HandleAsync(string body, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var type = root.GetProperty("webhook_type").GetString();
        var code = root.GetProperty("webhook_code").GetString();
        var itemId = root.TryGetProperty("item_id", out var item) ? item.GetString() : null;

        if (itemId is null)
            return;

        var connection = await _db.BankConnections.FirstOrDefaultAsync(
            x => x.PlaidItemId == itemId && !x.IsDeleted,
            ct);

        if (connection is null)
        {
            _logger.LogInformation("Plaid webhook {Type}/{Code} for an unknown item.", type, code);
            return;
        }

        switch (type, code)
        {
            case ("TRANSACTIONS", "SYNC_UPDATES_AVAILABLE"):
                if (connection.Status == (int)BankConnectionStatus.Connected &&
                    await _entitlements.GetPlanAsync(connection.UserId, ct) >= SubscriptionPlan.Bank)
                {
                    _syncQueue.Enqueue(connection.Id);
                }
                return;

            case ("ITEM", "ERROR") when ErrorCode(root) == "ITEM_LOGIN_REQUIRED":
            case ("ITEM", "PENDING_EXPIRATION"):
            case ("ITEM", "PENDING_DISCONNECT"):
                SetStatus(connection, BankConnectionStatus.ExpiredLink,
                    "Your bank needs you to log in again. Use Reconnect to keep syncing.");
                break;

            case ("ITEM", "LOGIN_REPAIRED"):
                SetStatus(connection, BankConnectionStatus.Connected, null);
                _syncQueue.Enqueue(connection.Id);
                break;

            case ("ITEM", "USER_PERMISSION_REVOKED"):
            case ("ITEM", "USER_ACCOUNT_REVOKED"):
                SetStatus(connection, BankConnectionStatus.Disconnected,
                    "Access was revoked at your bank. Connect the bank again to resume syncing.");
                break;

            default:
                return;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Plaid webhook {Type}/{Code} set connection {ConnectionId} to {Status}.",
            type,
            code,
            connection.Id,
            (BankConnectionStatus)connection.Status);
    }

    private static void SetStatus(
        Domain.Entities.BankConnection connection,
        BankConnectionStatus status,
        string? message)
    {
        connection.Status = (int)status;
        connection.ErrorMessage = message;
        connection.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? ErrorCode(JsonElement root) =>
        root.TryGetProperty("error", out var error) &&
        error.ValueKind == JsonValueKind.Object &&
        error.TryGetProperty("error_code", out var code)
            ? code.GetString()
            : null;
}
