using FinanceManager.Api.Filters;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

// Imports OFX/QFX statements downloaded from online banking (the
// "Money", "Quicken" or "OFX" download most Canadian banks offer).
[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/transactions/import/ofx")]
public sealed class OfxImportController : ControllerBase
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private const long MaximumRequestSize = MaximumFileSize + 64 * 1024;
    private static readonly string[] Extensions = [".ofx", ".qfx"];

    private readonly IOfxTransactionImportService _import;
    private readonly UserManager<ApplicationUser> _users;

    public OfxImportController(IOfxTransactionImportService import, UserManager<ApplicationUser> users)
    {
        _import = import;
        _users = users;
    }

    [HttpPost]
    [RequestSizeLimit(MaximumRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumRequestSize)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile? file,
        [FromForm] Guid accountId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(_users.GetUserId(User), out var userId))
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Select a non-empty OFX or QFX file." });
        if (file.Length > MaximumFileSize)
            return BadRequest(new { message = "Statement files cannot exceed 5 MB." });
        if (!Extensions.Contains(Path.GetExtension(file.FileName), StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "The file must have a .ofx or .qfx extension." });
        if (accountId == Guid.Empty)
            return BadRequest(new { message = "Choose the account this statement belongs to." });

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _import.ImportAsync(userId, accountId, stream, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (FormatException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
