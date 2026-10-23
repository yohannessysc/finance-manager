
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Identity;

public sealed class ShortLivedPasswordResetOptions
    : DataProtectionTokenProviderOptions
{
}

public sealed class ShortLivedPasswordResetProvider
    : DataProtectorTokenProvider<ApplicationUser>
{
    public ShortLivedPasswordResetProvider(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ShortLivedPasswordResetOptions> options,
        ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
        : base(
            dataProtectionProvider,
            Microsoft.Extensions.Options.Options.Create(
                new DataProtectionTokenProviderOptions
                {
                    Name = options.Value.Name,
                    TokenLifespan = options.Value.TokenLifespan
                }),
            logger)
    {
    }
}
