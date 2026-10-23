
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
}
