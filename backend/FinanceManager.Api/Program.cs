
using System.Threading.RateLimiting;

using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Api.BackgroundServices;
using FinanceManager.Api.Filters;

using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;

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

// Default categories for new users
builder.Services.AddScoped<DefaultCategorySeeder>();

// ---------------------------------------
// 10. Real Email Delivery — Resend
// ---------------------------------------
builder.Services.AddHttpClient<
    IEmailService,
    ResendEmailService>();

// ---------------------------------------
// 11. Background Services
// ---------------------------------------
builder.Services.AddHostedService<
    RecurringPostingBackgroundService>();

// ---------------------------------------
// 12. Authorization
// ---------------------------------------
builder.Services.AddAuthorization();

// ---------------------------------------
// 13. Build Application
// ---------------------------------------
var app = builder.Build();

// ---------------------------------------
// 14. Middleware Pipeline
// ---------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
