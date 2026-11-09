using System.Text;
using FinanceManager.Api.Filters;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/transactions/import/csv")]
public sealed class TransactionImportController : ControllerBase
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private const long MaximumRequestSize = MaximumFileSize + 64 * 1024;
    private readonly ICsvTransactionImportService _importService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TransactionImportController(
        ICsvTransactionImportService importService,
        UserManager<ApplicationUser> userManager)
    {
        _importService = importService;
        _userManager = userManager;
    }

    [HttpGet("template")]
    public IActionResult GetTemplate()
    {
        const string template =
            "date,description,amount,account,currency,type,category,merchant\r\n";

        return File(
            Encoding.UTF8.GetBytes(template),
            "text/csv",
            "transactions-template.csv");
    }

    [HttpPost]
    [RequestSizeLimit(MaximumRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumRequestSize)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Select a non-empty CSV file." });

        if (file.Length > MaximumFileSize)
            return BadRequest(new { message = "CSV files cannot exceed 5 MB." });

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "The uploaded file must have a .csv extension." });
        }

        var userValue = _userManager.GetUserId(User);
        if (!Guid.TryParse(userValue, out var userId))
            return Unauthorized();

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _importService.ImportAsync(
                userId,
                stream,
                ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (FormatException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
