
namespace FinanceManager.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(
        string email,
        string confirmationUrl,
        CancellationToken ct = default);

    Task SendPasswordResetAsync(
        string email,
        string resetUrl,
        CancellationToken ct = default);

    Task SendEmailChangeAsync(
        string email,
        string confirmationUrl,
        CancellationToken ct = default);

    Task SendHouseholdInvitationAsync(
        string email,
        string householdName,
        string invitationUrl,
        CancellationToken ct = default);

    Task SendSavingsGoalMilestoneAsync(
        string email,
        string goalName,
        int milestonePercent,
        CancellationToken ct = default);
}
