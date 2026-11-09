# Authentication Cookie Debugging Guide

## Problem Statement
After successful login (which sets `FinanceManager.Auth` cookie), the subsequent CSRF GET request clears that cookie, causing `/me` to return 401 Unauthorized.

**Screenshots confirm:**
1. POST `/api/v1/auth/login` → 200 OK (auth cookie set) ✅
2. GET `/api/v1/security/csrf` → 200 OK (includes auth cookie in request, but response clears it) ❌
3. GET `/api/v1/auth/me` → 401 Unauthorized (auth cookie gone) ❌

## Root Cause Analysis

### The Cookie Clearing Mechanism
The auth cookie is only cleared when `OnValidatePrincipal` event handler (in [Program.cs](/backend/FinanceManager.Api/Program.cs:105)) calls:
```csharp
context.RejectPrincipal();
await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
```

This happens when **session validation fails** after checking:
1. Identity security-stamp validation (ASP.NET Core)
2. `fm_sid` claim exists and parses as valid GUID
3. `NameIdentifier` claim exists and parses as valid GUID
4. `UserSessionService.ValidateAsync()` returns true

If **any** of these checks fail, the principal is rejected and the auth cookie is cleared.

### Why Validation Might Fail

The CSRF GET endpoint will trigger `OnValidatePrincipal` because the browser sends the auth cookie. Possible failure points:

1. **Session not found in database** - The `fm_sid` claim doesn't match any session
2. **Session expired** - `ExpiresAtUtc <= DateTime.UtcNow`
3. **Session revoked** - `RevokedAtUtc` is not null
4. **Security stamp invalid** - ASP.NET Core Identity detected a mismatch
5. **Claims missing or malformed** - `fm_sid` or `NameIdentifier` not present or invalid GUID

## Diagnostic Logging Added

### 1. OnValidatePrincipal Logging (Program.cs)
Logs on every auth cookie validation:
- `OnValidatePrincipal START: path={path}, method={method}` - Beginning validation
- `Principal is null after security-stamp validation` - Failed before our checks
- `Principal has {claimCount} claims` - Number of claims in ticket
- `sessionClaim={sessionClaim}, userClaim={userClaim}` - The actual claim values
- `ValidateAsync returned {valid}` - Session lookup result
- `Session validation PASSED` - Successful validation
- `Session validation failed, rejecting principal` - Failed validation (cookie will be cleared)

### 2. UserSessionService.ValidateAsync Logging
Logs database session lookup result:
- `Session not found for sessionId={sessionId}, userId={userId}` - No matching session
- `Session revoked at {revokedAt}` - Session was manually revoked
- `Session expired. ExpiresAt={expiresAt}, Now={now}` - Time-based expiration
- `Session valid` - Session passed all checks

## Troubleshooting Steps

### Step 1: Monitor Logs During Login
1. Start API: `dotnet run --project backend/FinanceManager.Api --launch-profile http`
2. In browser, open DevTools Network tab
3. Attempt login
4. Watch API logs for:
   - Successful user lookup
   - Session INSERT into database
   - `OnValidatePrincipal START: path=/api/v1/security/csrf` log

### Step 2: Check Login Success
Verify that login returns 200, not 401:
- If 401 with `INVALID_CREDENTIALS`: Email/password mismatch
- If 403 with `EMAIL_NOT_VERIFIED`: Email confirmation required
- If 423 with `ACCOUNT_LOCKED`: Too many failed attempts

### Step 3: Watch CSRF Request Logs
The GET `/csrf` request **must** trigger `OnValidatePrincipal`.  
Expected log sequence:
```
info: OnValidatePrincipal START: path=/api/v1/security/csrf, method=GET
info: Principal has {N} claims
info: sessionClaim={fm_sid_value}, userClaim={user_id}
info: ValidateAsync returned true  ← If this is true, no problem!
info: Session validation PASSED
```

If you see `ValidateAsync returned false`, check which log follows:
- `Session not found` → The fm_sid claim doesn't match database
- `Session revoked` → Session was explicitly revoked
- `Session expired` → 30-minute lifetime exceeded
- No message → Database error

### Step 4: Inspect Database Session
After login, run:
```sql
SELECT * FROM "UserSessions" ORDER BY "CreatedAtUtc" DESC LIMIT 1;
```

Verify:
- Row exists for logged-in user
- `ExpiresAtUtc` is in the future
- `RevokedAtUtc` is null

### Step 5: Check Claims in Cookie
To see what's actually in the auth cookie, add this line to `OnValidatePrincipal`:
```csharp
logger.LogInformation("Claims in principal: {claims}",
    string.Join(", ", context.Principal.Claims.Select(c => $"{c.Type}={c.Value}")));
```

Then search logs for claims during CSRF request.

## Common Issues & Fixes

### Issue: "Session not found"
**Cause:** `fm_sid` claim is empty, malformed, or doesn't match database

**Fix:**
1. Verify login creates session: Check DB for `UserSessions` row
2. Verify claim is added: Search logs for `sessionClaim=` in CSRF request
3. Check GUID format: Ensure both sessionId and userId parse correctly

### Issue: "Session expired"  
**Cause:** More than 30 minutes have passed since login

**Fix:** Only login minutes before testing CSRF

### Issue: "Principal is null after security-stamp validation"
**Cause:** ASP.NET Core Identity rejected the ticket

**Possible reasons:**
- Security stamp changed (user changed password/email)
- Data Protection keys mismatch (different machine/process)
- Cookie corrupted or unreadable

**Fix:**
1. Verify no concurrent password changes
2. Check Data Protection key location (should be at `~/.aspnet/` on dev machine)
3. Clear browser cookies and try again

### Issue: No "OnValidatePrincipal" logs appear
**Cause:** Auth cookie is not being sent

**Check:**
1. Browser DevTools → Application → Cookies → Verify `FinanceManager.Auth` exists
2. Verify cookie scope is correct: Domain should be `localhost` (not blank)
3. Verify cookie is not HttpOnly-only (shouldn't matter for server-side validation)
4. Check if `SameSite=Strict` is blocking cookie in requests

## Next Steps

1. **Reproduce the issue** using the frontend or curl with this flow:
   ```bash
   # Get CSRF token
   TOKEN=$(curl -s http://localhost:5070/api/v1/security/csrf | jq -r .token)
   
   # Login
   curl -c cookies.txt -X POST http://localhost:5070/api/v1/auth/login \
     -H "X-XSRF-TOKEN: $TOKEN" \
     -d '{"email":"user@example.com","password":"..."}'
   
   # Call CSRF (this should show validation logs)
   curl -b cookies.txt http://localhost:5070/api/v1/security/csrf
   
   # Call /me
   curl -b cookies.txt http://localhost:5070/api/v1/auth/me
   ```

2. **Capture full API logs** during the sequence and look for the specific failure point

3. **Share logs** showing:
   - Login response
   - OnValidatePrincipal logs from CSRF request
   - ValidateAsync logs (if session lookup failed)
   - Any error messages

## Technical Details

- **Auth Cookie:** `FinanceManager.Auth` (HttpOnly, SameSite=Lax, 30-min lifetime)
- **Antiforgery Cookie:** `FinanceManager.Antiforgery` (separate, also HttpOnly)
- **Session Claim:** `fm_sid` (added to principal during login)
- **Validation Runs On:** Every request with an auth cookie
- **Validation Order:**
  1. ASP.NET Core security-stamp check
  2. Our custom `fm_sid` and `NameIdentifier` claims validation
  3. Our custom `UserSessionService.ValidateAsync()` check

## Files Changed
- [Program.cs](/backend/FinanceManager.Api/Program.cs) - Added detailed OnValidatePrincipal logging
- [UserSessionService.cs](/backend/FinanceManager.Infrastructure/Services/UserSessionService.cs) - Added ValidateAsync logging, injected ILogger
