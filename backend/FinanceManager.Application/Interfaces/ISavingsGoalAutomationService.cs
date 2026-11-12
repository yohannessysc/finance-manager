namespace FinanceManager.Application.Interfaces;

public interface ISavingsGoalAutomationService
{
    // Posts due scheduled contributions. Returns how many were posted.
    Task<int> ProcessScheduledContributionsAsync(
        CancellationToken ct = default);

    // Emails members when a goal crosses 25/50/75/100%.
    // Returns how many goals had a milestone notification sent.
    Task<int> SendMilestoneNotificationsAsync(
        CancellationToken ct = default);
}
