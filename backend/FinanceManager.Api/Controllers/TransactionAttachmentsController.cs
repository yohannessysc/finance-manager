using FinanceManager.Api.Filters;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/transactions/{transactionId:guid}/attachments")]
public sealed class TransactionAttachmentsController : ControllerBase
{
    private const long MaximumRequestSize = TransactionAttachmentService.MaximumFileSize + 64 * 1024;

    private readonly ITransactionAttachmentService _attachments;
    private readonly UserManager<ApplicationUser> _users;

    public TransactionAttachmentsController(ITransactionAttachmentService attachments, UserManager<ApplicationUser> users)
    {
        _attachments = attachments;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid transactionId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var attachments = await _attachments.ListAsync(transactionId, userId, ct);
        return attachments is null ? NotFound() : Ok(attachments);
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid transactionId, Guid attachmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var file = await _attachments.GetAsync(transactionId, attachmentId, userId, ct);
        if (file is null)
            return NotFound();

        // Uploaded content: never sniffed, never cached, and sandboxed so
        // it cannot run script on this origin. Photos open in the browser;
        // PDFs download (browser PDF viewers don't run under a sandbox).
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; img-src 'self'";

        var inline = file.ContentType.StartsWith("image/", StringComparison.Ordinal);
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();

        return File(file.Data, file.ContentType);
    }

    [HttpPost]
    [RequestSizeLimit(MaximumRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumRequestSize)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(Guid transactionId, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a photo or PDF of the receipt." });

        try
        {
            await using var stream = file.OpenReadStream();
            var attachment = await _attachments.AddAsync(transactionId, userId, file.FileName, stream, ct);
            return attachment is null ? NotFound() : Ok(attachment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid transactionId, Guid attachmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return await _attachments.DeleteAsync(transactionId, attachmentId, userId, ct)
            ? NoContent()
            : NotFound();
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(_users.GetUserId(User), out userId);
}
