
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;

using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Api.BackgroundServices;
using FinanceManager.Api.Filters;

using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Billing;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Security;
using FinanceManager.Infrastructure.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------
// 0. Logging and error tracking
// ---------------------------------------
// Production logs are single-line JSON so a log platform can index them.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

// Sentry stays disabled until Sentry:Dsn is set. Personal data is never
// sent: this is a finance app, so no request bodies, cookies or user IPs.
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? "";
    options.Environment = builder.Environment.EnvironmentName;
    options.SendDefaultPii = false;
    options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
    options.TracesSampleRate =
        builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? 0;
});

builder.Services.AddProblemDetails();

// Behind the reverse proxy the client IP and scheme arrive in
// X-Forwarded-* headers. Without this, every user shares the proxy's IP
// for login rate limiting. The API must only be reachable through the
// proxy, otherwise clients could spoof these headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit =
        builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<FinanceDbContext>("database", tags: ["ready"]);

// ---------------------------------------
// 1. Controllers and API documentation
// ---------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ---------------------------------------
// 2. PostgreSQL / Entity Framework Core
// ---------------------------------------
builder.Services.AddDbContext<FinanceDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"));
});

// ---------------------------------------
// 2b. Data Protection
// ---------------------------------------
// Keys live in the database so every instance shares them and auth
// cookies, CSRF tokens and encrypted bank tokens survive restarts.
// Outside development the keys themselves must be encrypted with a
// certificate kept outside the database, or a database leak would
// expose the bank tokens too.
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("FinanceManager")
    .PersistKeysToDbContext<FinanceDbContext>();

var keyCertificatePath = builder.Configuration["DataProtection:CertificatePath"];
if (!string.IsNullOrWhiteSpace(keyCertificatePath))
{
    dataProtection.ProtectKeysWithCertificate(
        X509CertificateLoader.LoadPkcs12FromFile(
            keyCertificatePath,
            builder.Configuration["DataProtection:CertificatePassword"]));
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "DataProtection:CertificatePath must be configured outside development.");
}

builder.Services.AddSingleton<IBankTokenProtector, BankTokenProtector>();

// ---------------------------------------
// 3. ASP.NET Core Identity
// ---------------------------------------
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.User.RequireUniqueEmail = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.SignIn.RequireConfirmedEmail = true;

        // Separate 30-minute password-reset tokens.
        options.Tokens.PasswordResetTokenProvider =
            "ShortLivedPasswordReset";
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<FinanceDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddTokenProvider<ShortLivedPasswordResetProvider>(
        "ShortLivedPasswordReset");

// ---------------------------------------
// 4. Password Reset Token Expiration
// ---------------------------------------
builder.Services.Configure<ShortLivedPasswordResetOptions>(
    options =>
    {
        options.Name = "FinanceManagerPasswordReset";
        options.TokenLifespan = TimeSpan.FromMinutes(30);
    });

// ---------------------------------------
// 5. Cookie Authentication
// ---------------------------------------
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "FinanceManager.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

    options.ExpireTimeSpan = UserSessionService.Lifetime;
    // Cookie slides with activity; the server-side session enforces
    // the idle timeout and the absolute cap (UserSessionService).
    options.SlidingExpiration = true;

    // Keep Identity's security-stamp validation, then also require
    // an active server-side session that matches the cookie.
    var validateSecurityStamp = options.Events.OnValidatePrincipal;

    options.Events.OnValidatePrincipal = async context =>
    {
        await validateSecurityStamp(context);

        if (context.Principal is null)
            return;

        var sessionClaim = context.Principal
            .FindFirst(UserSessionService.SessionClaimType)?.Value;

        var userClaim = context.Principal
            .FindFirst(System.Security.Claims.ClaimTypes
                .NameIdentifier)?.Value;

        var valid =
            Guid.TryParse(sessionClaim, out var sessionId) &&
            Guid.TryParse(userClaim, out var userId) &&
            await context.HttpContext.RequestServices
                .GetRequiredService<UserSessionService>()
                .ValidateAsync(
                    sessionId,
                    userId,
                    context.HttpContext.RequestAborted);

        if (!valid)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                IdentityConstants.ApplicationScheme);
        }
    };

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        return Task.CompletedTask;
    };
});

// The short-lived cookie that carries a user between the password step
// and the two-factor code step of sign-in.
builder.Services.Configure<CookieAuthenticationOptions>(
    IdentityConstants.TwoFactorUserIdScheme,
    options =>
    {
        options.Cookie.Name = "FinanceManager.TwoFactor";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy =
            builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    });

// ---------------------------------------
// 6. Security Stamp Validation
// ---------------------------------------

// Validate the security stamp on every authenticated
// request instead of using the default periodic interval.
//
// Password resets update the user's security stamp.
// Existing session cookies with the old stamp are
// rejected on their next authenticated request.
builder.Services.Configure<SecurityStampValidatorOptions>(
    options =>
    {
        options.ValidationInterval = TimeSpan.Zero;
    });

// ---------------------------------------
// 7. CSRF / Antiforgery
// ---------------------------------------
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";

    options.Cookie.Name = "FinanceManager.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

builder.Services.AddScoped<ValidateCsrfTokenFilter>();

// ---------------------------------------
// 8. Authentication Rate Limiting
// ---------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("auth-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("auth-recovery", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("auth-verification", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ---------------------------------------
// 9. Clean Architecture Dependencies
// ---------------------------------------

// Financial accounts
builder.Services.AddScoped<
    IFinancialAccountRepository,
    FinancialAccountRepository>();

builder.Services.AddScoped<
    IFinancialAccountService,
    FinancialAccountService>();

// Financial transactions
builder.Services.AddScoped<
    IFinancialTransactionRepository,
    FinancialTransactionRepository>();

builder.Services.AddScoped<
    IFinancialTransactionService,
    FinancialTransactionService>();

builder.Services.AddScoped<
    ICsvTransactionImportService,
    CsvTransactionImportService>();

// Recurring transactions
builder.Services.AddScoped<
    IRecurringTransactionRepository,
    RecurringTransactionRepository>();

builder.Services.AddScoped<
    IRecurringTransactionService,
    RecurringTransactionService>();

builder.Services.AddScoped<
    IRecurringPostingService,
    RecurringPostingService>();

// Categories
builder.Services.AddScoped<
    ITransactionCategoryRepository,
    TransactionCategoryRepository>();

builder.Services.AddScoped<
    ITransactionCategoryService,
    TransactionCategoryService>();

// Account balances
builder.Services.AddScoped<
    IAccountBalanceService,
    AccountBalanceService>();

// Dashboard
builder.Services.AddScoped<
    IDashboardService,
    DashboardService>();

builder.Services.AddScoped<
    IBudgetService,
    BudgetService>();

builder.Services.AddScoped<
    ISavingsGoalService,
    SavingsGoalService>();

builder.Services.AddScoped<
    ISavingsGoalAutomationService,
    SavingsGoalAutomationService>();

builder.Services.AddScoped<
    IHouseholdAuthorizationService,
    HouseholdAuthorizationService>();

builder.Services.AddScoped<
    IHouseholdService,
    HouseholdService>();

// Default categories for new users
builder.Services.AddScoped<DefaultCategorySeeder>();

// Server-side login sessions
builder.Services.AddScoped<UserSessionService>();

// Billing (Stripe) and plan entitlements
builder.Services.Configure<BillingOptions>(
    builder.Configuration.GetSection(BillingOptions.SectionName));

var defaultPlan = builder.Configuration
    .GetSection(BillingOptions.SectionName)
    .GetValue<SubscriptionPlan?>(nameof(BillingOptions.DefaultPlan));
if (defaultPlan is not (null or SubscriptionPlan.Free) &&
    !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Billing:DefaultPlan gives every user paid features and is only allowed in development.");
}

builder.Services.AddSingleton<IStripeGateway, StripeGateway>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<IBillingService>(x => x.GetRequiredService<BillingService>());
builder.Services.AddScoped<IEntitlementService>(x => x.GetRequiredService<BillingService>());

// Bank integration services
builder.Services.Configure<FinanceManager.Infrastructure.Services.Plaid.PlaidOptions>(
    builder.Configuration.GetSection(
        FinanceManager.Infrastructure.Services.Plaid.PlaidOptions.SectionName));

builder.Services.AddHttpClient<FinanceManager.Infrastructure.Services.Plaid.IPlaidService,
    FinanceManager.Infrastructure.Services.Plaid.PlaidService>();

builder.Services.AddScoped<IBankConnectionService, BankConnectionService>();
builder.Services.AddSingleton<IBankSyncQueue, BankSyncQueue>();
builder.Services.AddScoped<FinanceManager.Infrastructure.Services.Plaid.PlaidWebhookVerifier>();
builder.Services.AddScoped<FinanceManager.Infrastructure.Services.Plaid.PlaidWebhookHandler>();
builder.Services.AddScoped<IBankSyncService, BankSyncService>();
builder.Services.AddScoped<ICategoryRuleService, CategoryRuleService>();

// ---------------------------------------
// 10. Real Email Delivery — Resend
// ---------------------------------------
builder.Services.AddHttpClient<
    IEmailService,
    ResendEmailService>();

// ---------------------------------------
// 11. Background Services
// ---------------------------------------
builder.Services.AddSingleton<IJobLock, PostgresJobLock>();

builder.Services.AddHostedService<
    RecurringPostingBackgroundService>();

builder.Services.AddHostedService<
    FinanceManager.Api.BackgroundServices.BankSyncBackgroundService>();

builder.Services.AddHostedService<
    SavingsGoalBackgroundService>();

builder.Services.AddHostedService<BankSyncQueueWorker>();

// ---------------------------------------
// 12. Authorization
// ---------------------------------------
builder.Services.AddAuthorization();

// ---------------------------------------
// 13. Build Application
// ---------------------------------------
var app = builder.Build();

app.UseForwardedHeaders();

await BankTokenEncryptionBackfill.RunAsync(app.Services);

// ---------------------------------------
// 14. Middleware Pipeline
// ---------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Unhandled errors become a ProblemDetails JSON response carrying a
    // traceId (to match logs and Sentry) instead of an empty 500.
    app.UseExceptionHandler();
    app.UseHsts();
}

// No UseHttpsRedirection: TLS terminates at the reverse proxy or load
// balancer, which owns the HTTP -> HTTPS redirect. HSTS still applies.

// The API only ever returns JSON, so it can deny everything a browser
// might otherwise do with a response.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseRouting();

app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

// Liveness: the process is up. Readiness: it can also reach the
// database, so a load balancer only routes traffic to working instances.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();

// Lets the integration tests host the app with WebApplicationFactory.
public partial class Program;
