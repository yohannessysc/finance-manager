
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceManager.Api.Filters;

public class ValidateCsrfTokenFilter : IAsyncActionFilter
{
    private readonly IAntiforgery _antiforgery;

    public ValidateCsrfTokenFilter(
        IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;

        if (HttpMethods.IsGet(method) ||
            HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method))
        {
            await next();
            return;
        }

        var isValid = await _antiforgery.IsRequestValidAsync(
            context.HttpContext);

        if (!isValid)
        {
            context.Result = new BadRequestObjectResult(
                new { message = "Invalid or missing CSRF token." });

            return;
        }

        await next();
    }
}
