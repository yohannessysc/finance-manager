
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;

using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Repositories;

using FinanceManager.Api.Filters;

using Microsoft.AspNetCore.Identity;
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
        // Password requirements
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Require unique email addresses
        options.User.RequireUniqueEmail = true;

        // Account lockout
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<FinanceDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ---------------------------------------
// 4. Cookie Authentication
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
// 5. CSRF / Antiforgery Configuration
// ---------------------------------------
builder.Services.AddAntiforgery(options =>
{
    // Header used by Angular
    options.HeaderName = "X-XSRF-TOKEN";

    // Antiforgery cookie
    options.Cookie.Name = "FinanceManager.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

// Register our custom CSRF validation filter
builder.Services.AddScoped<ValidateCsrfTokenFilter>();

// ---------------------------------------
// 6. Clean Architecture Dependencies
// ---------------------------------------
builder.Services.AddScoped<
    IFinancialAccountRepository,
    FinancialAccountRepository>();

builder.Services.AddScoped<
    IFinancialAccountService,
    FinancialAccountService>();

// ---------------------------------------
// 7. Authorization
// ---------------------------------------
builder.Services.AddAuthorization();

// ---------------------------------------
// 8. Build Application
// ---------------------------------------
var app = builder.Build();

// ---------------------------------------
// 9. Middleware Pipeline
// ---------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
