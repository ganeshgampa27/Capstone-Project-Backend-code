using ResumeBuilderBackend.Models;

namespace ResumeBuilderBackend.Services
{
    public interface IEmailservice
    {
        Task SendEmail(MailRequest request);
        Task SendEmailAsync(string email, string subject, string message);

    }
}
