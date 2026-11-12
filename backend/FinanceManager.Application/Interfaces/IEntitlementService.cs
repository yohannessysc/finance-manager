using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Interfaces;

public interface IEntitlementService
{
    Task<SubscriptionPlan> GetPlanAsync(
        Guid userId,
        CancellationToken ct = default);
}
