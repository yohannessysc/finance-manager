using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

public sealed class HouseholdMember
{
    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }

    public HouseholdRole Role { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
