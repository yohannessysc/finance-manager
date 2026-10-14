
using Microsoft.AspNetCore.Identity;

namespace FinanceManager.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
