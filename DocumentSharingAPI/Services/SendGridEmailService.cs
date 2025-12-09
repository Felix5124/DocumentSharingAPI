using SendGrid;
using SendGrid.Helpers.Mail;

namespace DocumentSharingAPI.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SendGridEmailService> _logger;

        public SendGridEmailService(IConfiguration configuration, ILogger<SendGridEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            try
            {
                var apiKey = _configuration["Email:SendGridApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("SendGrid API Key is not configured");
                    throw new InvalidOperationException("SendGrid API Key is not configured");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(
                    _configuration["Email:FromEmail"] ?? "noreply@yourdomain.com",
                    _configuration["Email:FromName"] ?? "Document Sharing"
                );
                var to = new EmailAddress(toEmail);
                var subject = "Xác thực email - Document Sharing";

                // HTML Email Template
                var htmlContent = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                        <style>
                            body {{
                                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                                line-height: 1.6;
                                color: #333;
                                background-color: #f4f4f4;
                                margin: 0;
                                padding: 0;
                            }}
                            .container {{
                                max-width: 600px;
                                margin: 30px auto;
                                background-color: #ffffff;
                                border-radius: 10px;
                                box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                                overflow: hidden;
                            }}
                            .header {{
                                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                                color: white;
                                padding: 30px;
                                text-align: center;
                            }}
                            .header h1 {{
                                margin: 0;
                                font-size: 28px;
                            }}
                            .content {{
                                padding: 40px 30px;
                            }}
                            .content h2 {{
                                color: #333;
                                margin-top: 0;
                            }}
                            .content p {{
                                margin: 15px 0;
                                color: #555;
                            }}
                            .button-container {{
                                text-align: center;
                                margin: 35px 0;
                            }}
                            .verify-button {{
                                display: inline-block;
                                background: #333;
                                color: white;
                                text-decoration: none;
                                padding: 15px 40px;
                                border-radius: 50px;
                                font-weight: bold;
                                font-size: 16px;
                                transition: all 0.3s ease;
                            }}
                            .verify-button:hover {{
                                box-shadow: 0 6px 20px rgba(102, 126, 234, 0.6);
                                transform: translateY(-2px);
                            }}
                            .info-box {{
                                background-color: #f8f9fa;
                                border-left: 4px solid #667eea;
                                padding: 15px;
                                margin: 20px 0;
                                border-radius: 5px;
                            }}
                            .footer {{
                                background-color: #f8f9fa;
                                padding: 20px;
                                text-align: center;
                                color: #666;
                                font-size: 14px;
                                border-top: 1px solid #e0e0e0;
                            }}
                            .footer p {{
                                margin: 5px 0;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>📚 Document Sharing</h1>
                            </div>
                            <div class='content'>
                                <h2>Xác thực Email của bạn</h2>
                                <p>Xin chào,</p>
                                <p>Cảm ơn bạn đã đăng ký tài khoản tại <strong>Document Sharing</strong>. Để hoàn tất quá trình đăng ký, vui lòng xác thực địa chỉ email của bạn bằng cách nhấp vào nút bên dưới:</p>
                                
                                <div class='button-container'>
                                    <a href='{verificationLink}' style='display: inline-block; background: #333; color: white; text-decoration: none; padding: 15px 40px; border-radius: 50px; font-weight: bold; font-size: 16px;'>
                                        ✓ Xác thực Email
                                    </a>
                                </div>

                                <div class='info-box'>
                                    <p style='margin: 0;'><strong>⏰ Lưu ý:</strong> Link xác thực này sẽ hết hạn sau <strong>24 giờ</strong>.</p>
                                </div>

                                <p>Nếu bạn không thể nhấp vào nút, hãy sao chép và dán link sau vào trình duyệt:</p>
                                <p style='word-break: break-all; color: #667eea; font-size: 14px;'>{verificationLink}</p>

                                <p style='margin-top: 30px; color: #999; font-size: 14px;'>Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
                            </div>
                            <div class='footer'>
                                <p><strong>Document Sharing</strong></p>
                                <p>Trường Đại Học Hutech, Tp. Hồ Chí Minh</p>
                                <p style='margin-top: 15px;'>© 2024 Document Sharing. All rights reserved.</p>
                                <p>Email này được gửi tự động, vui lòng không trả lời.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                // Plain text version for email clients that don't support HTML
                var plainTextContent = $@"
Xác thực Email - Document Sharing

Xin chào,

Cảm ơn bạn đã đăng ký tài khoản tại Document Sharing.

Vui lòng truy cập link sau để xác thực email của bạn:
{verificationLink}

Link này sẽ hết hạn sau 24 giờ.

Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.

---
© 2024 Document Sharing. All rights reserved.
                ";

                var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Verification email sent successfully to {toEmail}");
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    _logger.LogError($"Failed to send email. Status: {response.StatusCode}, Body: {responseBody}");
                    throw new Exception($"Failed to send email: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending verification email to {toEmail}");
                throw;
            }
        }
    }
}
