using System.Security.Claims;

using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Services;

using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Api;

// The server-side session id is a per-device cookie claim. Identity rebuilds
// principals from the user record in several places; these keep the claim.
public static class SessionClaims
{
    public static void CopySessionId(ClaimsPrincipal? from, ClaimsPrincipal? to)
    {
        var sessionId = from?.FindFirst(UserSessionService.SessionClaimType);

        if (sessionId is not null &&
            to?.Identity is ClaimsIdentity identity &&
            !identity.HasClaim(c => c.Type == UserSessionService.SessionClaimType))
        {
            identity.AddClaim(new Claim(sessionId.Type, sessionId.Value));
        }
    }

    // Like SignInManager.RefreshSignInAsync (needed after the security stamp
    // changes), but keeps this device's session id.
    public static Task RefreshSignInKeepingSessionAsync(
        this SignInManager<ApplicationUser> signInManager,
        ApplicationUser user,
        ClaimsPrincipal current)
    {
        var sessionId = current.FindFirst(UserSessionService.SessionClaimType);

        return signInManager.SignInWithClaimsAsync(
            user,
            isPersistent: false,
            sessionId is null ? [] : [new Claim(sessionId.Type, sessionId.Value)]);
    }
}
