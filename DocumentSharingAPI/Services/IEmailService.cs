namespace DocumentSharingAPI.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string verificationLink);
    }
}
