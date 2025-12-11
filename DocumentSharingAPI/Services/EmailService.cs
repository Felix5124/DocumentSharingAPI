using System.Net;
using System.Net.Mail;

namespace DocumentSharingAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["Email:Username"];
                var smtpPassword = _configuration["Email:Password"];
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"] ?? "Document Sharing";

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "Xác thực email - Document Sharing",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #333;'>Xác thực Email của bạn</h2>
                                <p>Xin chào,</p>
                                <p>Cảm ơn bạn đã đăng ký tài khoản tại Document Sharing. Vui lòng click vào link bên dưới để xác thực email của bạn:</p>
                                <div style='margin: 30px 0;'>
                                    <a href='{verificationLink}' style='background-color: #4CAF50; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                        Xác thực Email
                                    </a>
                                </div>
                                <p style='color: #666; font-size: 14px;'>Link này sẽ hết hạn sau 24 giờ.</p>
                                <p style='color: #666; font-size: 14px;'>Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
                                <hr style='margin: 30px 0; border: none; border-top: 1px solid #ddd;'>
                                <p style='color: #999; font-size: 12px;'>© 2024 Document Sharing. All rights reserved.</p>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Verification email sent to {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send verification email to {toEmail}");
                throw;
            }
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["Email:Username"];
                var smtpPassword = _configuration["Email:Password"];
                var fromEmail = _configuration["Email:FromEmail"];
                var fromName = _configuration["Email:FromName"] ?? "Document Sharing";

                using var smtpClient = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "Đặt lại mật khẩu - Document Sharing",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #333;'>Đặt lại mật khẩu</h2>
                                <p>Xin chào,</p>
                                <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Click vào link bên dưới để đặt lại mật khẩu:</p>
                                <div style='margin: 30px 0;'>
                                    <a href='{resetLink}' style='background-color: #f5576c; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                        Đặt lại mật khẩu
                                    </a>
                                </div>
                                <p style='color: #666; font-size: 14px;'>Link này sẽ hết hạn sau 1 giờ.</p>
                                <p style='color: #666; font-size: 14px;'>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                                <hr style='margin: 30px 0; border: none; border-top: 1px solid #ddd;'>
                                <p style='color: #999; font-size: 12px;'>© 2024 Document Sharing. All rights reserved.</p>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Password reset email sent to {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send password reset email to {toEmail}");
                throw;
            }
        }
    }
}
