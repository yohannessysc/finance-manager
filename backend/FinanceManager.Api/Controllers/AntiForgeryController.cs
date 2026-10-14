
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Route("api/v1/security")]
public class AntiforgeryController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryController(
        IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(
            HttpContext);

        Response.Headers.CacheControl = "no-store";

        return Ok(new
        {
            token = tokens.RequestToken
        });
    }
}
