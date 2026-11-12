using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceManager.Api.Filters;

// Rejects the request with 403 PLAN_REQUIRED unless the user's
// subscription includes the given plan.
public sealed class RequiresPlanAttribute : TypeFilterAttribute
{
    public RequiresPlanAttribute(SubscriptionPlan plan)
        : base(typeof(RequiresPlanFilter))
    {
        Arguments = [plan];
    }

    private sealed class RequiresPlanFilter(
        SubscriptionPlan requiredPlan,
        IEntitlementService entitlements,
        UserManager<ApplicationUser> users) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (!Guid.TryParse(users.GetUserId(context.HttpContext.User), out var userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var plan = await entitlements.GetPlanAsync(
                userId,
                context.HttpContext.RequestAborted);

            if (plan < requiredPlan)
            {
                context.Result = new ObjectResult(new
                {
                    code = "PLAN_REQUIRED",
                    requiredPlan,
                    message = $"This feature needs the {requiredPlan} plan. Upgrade on the Billing page."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
