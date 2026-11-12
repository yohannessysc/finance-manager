using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Bank;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinanceManager.Api.Controllers;

/// <summary>
/// Bank integration endpoints for linking bank accounts and managing connections
/// </summary>
[ApiController]
[Route("api/v1/bank")]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
public class BankConnectionsController : ControllerBase
{
    private readonly IBankConnectionService _bankConnectionService;
    private readonly IBankSyncService _bankSyncService;
    private readonly ILogger<BankConnectionsController> _logger;

    public BankConnectionsController(
        IBankConnectionService bankConnectionService,
        IBankSyncService bankSyncService,
        ILogger<BankConnectionsController> logger)
    {
        _bankConnectionService = bankConnectionService;
        _bankSyncService = bankSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Get Plaid Link token to initiate bank account linking
    /// </summary>
    /// <remarks>
    /// Frontend should initialize Plaid Link with the returned linkToken.
    /// After user selects account, exchange the public token for access token.
    /// </remarks>
    [HttpPost("plaid/link-token")]
    [RequiresPlan(SubscriptionPlan.Bank)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlaidLinkTokenResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlaidLinkTokenResponse>> CreatePlaidLinkToken(
        [FromBody] CreatePlaidLinkRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        try
        {
            _logger.LogInformation(
                "Creating Plaid Link token for user {UserId}, account {AccountId}",
                parsedUserId,
                request.FinancialAccountId);

            var response = await _bankConnectionService.CreatePlaidLinkTokenAsync(
                parsedUserId,
                request);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Plaid Link token for user {UserId}", parsedUserId);
            return BadRequest(new { error = "Could not start bank linking. Please try again." });
        }
    }

    /// <summary>
    /// Exchange Plaid public token for access token and save bank connection
    /// </summary>
    /// <remarks>
    /// Call this after user completes Plaid Link flow.
    /// The public token is only valid for 30 minutes.
    /// </remarks>
    [HttpPost("plaid/exchange-token")]
    [RequiresPlan(SubscriptionPlan.Bank)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BankConnectionResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BankConnectionResponse>> ExchangePlaidToken(
        [FromBody] ExchangePlaidTokenRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        if (string.IsNullOrEmpty(request.PublicToken))
        {
            return BadRequest(new { error = "Public token is required" });
        }

        try
        {
            _logger.LogInformation(
                "Exchanging Plaid token for user {UserId}, institution {Institution}",
                parsedUserId,
                request.Metadata?.InstitutionName);

            var response = await _bankConnectionService.ExchangePlaidTokenAsync(
                parsedUserId,
                request);

            _logger.LogInformation(
                "Successfully connected bank account for user {UserId}, connection {ConnectionId}",
                parsedUserId,
                response.Id);

            // First sync right away so accounts and transactions appear immediately.
            // If Plaid is still preparing history, the UI retries via the sync endpoint
            // and the background sync picks it up anyway.
            var syncLog = await _bankSyncService.SyncTransactionsAsync(response.Id);
            response.TransactionCount = syncLog.TransactionsImported;

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Plaid token for user {UserId}", parsedUserId);
            return BadRequest(new { error = "Could not finish connecting your bank. Please try again." });
        }
    }

    /// <summary>
    /// Get all bank connections for the current user
    /// </summary>
    [HttpGet("connections")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<BankConnectionResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<BankConnectionResponse>>> GetConnections()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        try
        {
            var connections = await _bankConnectionService.GetUserConnectionsAsync(parsedUserId);
            return Ok(connections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connections for user {UserId}", parsedUserId);
            return BadRequest(new { error = "Failed to retrieve connections" });
        }
    }

    /// <summary>
    /// Get a specific bank connection
    /// </summary>
    [HttpGet("connections/{connectionId}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BankConnectionResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BankConnectionResponse>> GetConnection(Guid connectionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        try
        {
            var connection = await _bankConnectionService.GetConnectionAsync(connectionId, parsedUserId);
            if (connection == null)
            {
                return NotFound();
            }

            return Ok(connection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection {ConnectionId} for user {UserId}", connectionId, parsedUserId);
            return BadRequest(new { error = "Failed to retrieve connection" });
        }
    }

    /// <summary>
    /// Disconnect a bank connection (safe to call multiple times)
    /// </summary>
    [HttpDelete("connections/{connectionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisconnectBankAccount(Guid connectionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        try
        {
            var result = await _bankConnectionService.DisconnectAsync(connectionId, parsedUserId);
            if (!result)
            {
                return NotFound();
            }

            _logger.LogInformation(
                "Disconnected bank connection {ConnectionId} for user {UserId}",
                connectionId,
                parsedUserId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect connection {ConnectionId} for user {UserId}", connectionId, parsedUserId);
            return BadRequest(new { error = "Failed to disconnect" });
        }
    }

    /// <summary>
    /// Get all transactions from a bank connection
    /// </summary>
    [HttpGet("connections/{connectionId}/transactions")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<BankTransactionResponse>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<BankTransactionResponse>>> GetConnectionTransactions(
        Guid connectionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { error = "Invalid pagination parameters" });
        }

        try
        {
            var transactions = await _bankConnectionService.GetConnectionTransactionsAsync(
                connectionId,
                parsedUserId,
                pageNumber,
                pageSize);

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transactions for connection {ConnectionId}", connectionId);
            return BadRequest(new { error = "Failed to retrieve transactions" });
        }
    }

    /// <summary>
    /// Manually trigger a sync for a specific connection
    /// </summary>
    /// <remarks>
    /// Normally syncs are done by background service on a schedule.
    /// This endpoint allows on-demand sync.
    /// </remarks>
    [HttpPost("connections/{connectionId}/sync")]
    [RequiresPlan(SubscriptionPlan.Bank)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncConnection(Guid connectionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return Unauthorized("Invalid user");
        }

        try
        {
            _logger.LogInformation(
                "Manually triggering sync for connection {ConnectionId}, user {UserId}",
                connectionId,
                parsedUserId);

            // Verify the connection belongs to the user
            var connection = await _bankConnectionService.GetConnectionAsync(connectionId, parsedUserId);
            if (connection == null)
            {
                return NotFound();
            }

            // Actually perform the sync
            var syncLog = await _bankSyncService.SyncTransactionsAsync(connectionId);

            return Ok(new
            {
                syncId = syncLog.Id,
                status = syncLog.Status,
                transactionsRetrieved = syncLog.TransactionsRetrieved,
                transactionsImported = syncLog.TransactionsImported,
                duplicatesSkipped = syncLog.DuplicatesSkipped,
                errorCount = syncLog.ErrorCount,
                completedAt = syncLog.CompletedAtUtc,
                message = syncLog.Status switch
                {
                    "Pending" => "Your bank is still preparing transaction history. Try again in a moment.",
                    "Failed" => syncLog.ErrorMessage ?? "Sync failed",
                    _ => "Sync completed"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync connection {ConnectionId} for user {UserId}", connectionId, parsedUserId);
            return BadRequest(new { error = "Sync failed. Please try again later." });
        }
    }

    /// <summary>
    /// Get a Plaid Link token in update mode for a connection whose bank
    /// login expired (status ExpiredLink).
    /// </summary>
    [HttpPost("connections/{connectionId}/reconnect-token")]
    [RequiresPlan(SubscriptionPlan.Bank)]
    public async Task<ActionResult<PlaidLinkTokenResponse>> CreateReconnectToken(Guid connectionId)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Unauthorized("Invalid user");

        try
        {
            var token = await _bankConnectionService.CreateReconnectLinkTokenAsync(connectionId, userId);
            return token is null ? NotFound() : Ok(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create reconnect token for connection {ConnectionId}", connectionId);
            return BadRequest(new { error = "Failed to start reconnecting this bank." });
        }
    }

    /// <summary>
    /// Called after Plaid Link update mode succeeds: marks the connection
    /// healthy and syncs right away.
    /// </summary>
    [HttpPost("connections/{connectionId}/reconnected")]
    [RequiresPlan(SubscriptionPlan.Bank)]
    public async Task<IActionResult> MarkReconnected(Guid connectionId)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Unauthorized("Invalid user");

        if (!await _bankConnectionService.MarkReconnectedAsync(connectionId, userId))
            return NotFound();

        var syncLog = await _bankSyncService.SyncTransactionsAsync(connectionId);
        return Ok(new
        {
            syncId = syncLog.Id,
            status = syncLog.Status,
            transactionsImported = syncLog.TransactionsImported
        });
    }
}
