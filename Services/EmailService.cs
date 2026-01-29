using System.Net;
using System.Net.Mail;

namespace SocialCalc.Web.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "25");
            var fromEmail = _configuration["Email:From"];

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = "Password Reset Request",
                    Body = GeneratePasswordResetEmailBody(resetLink),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Password reset email sent to: {email}");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending password reset email: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string email, string userName)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "25");
            var fromEmail = _configuration["Email:From"];

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = "Welcome to Social Calc",
                    Body = GenerateWelcomeEmailBody(userName),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Welcome email sent to: {email}");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending welcome email: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SendSheetSharedNotificationAsync(string email, string sheetName)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "25");
            var fromEmail = _configuration["Email:From"];

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = "Sheet Shared with You",
                    Body = GenerateSheetSharedEmailBody(sheetName),
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Sheet shared notification sent to: {email}");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending sheet notification email: {ex.Message}");
            return false;
        }
    }

    private string GeneratePasswordResetEmailBody(string resetLink)
    {
        return $@"
            <html>
            <body>
                <h2>Password Reset Request</h2>
                <p>You requested a password reset for your Social Calc account.</p>
                <p><a href='{resetLink}'>Click here to reset your password</a></p>
                <p>This link expires in 24 hours.</p>
                <p>If you didn't request this, please ignore this email.</p>
            </body>
            </html>";
    }

    private string GenerateWelcomeEmailBody(string userName)
    {
        return $@"
            <html>
            <body>
                <h2>Welcome to Social Calc!</h2>
                <p>Hi {userName},</p>
                <p>Your account has been successfully created.</p>
                <p>You can now start creating and managing your spreadsheets.</p>
                <p>Thank you for joining us!</p>
            </body>
            </html>";
    }

    private string GenerateSheetSharedEmailBody(string sheetName)
    {
        return $@"
            <html>
            <body>
                <h2>Sheet Shared with You</h2>
                <p>A spreadsheet named '{sheetName}' has been shared with you.</p>
                <p><a href='https://yourdomain.com/sheets'>View your sheets</a></p>
            </body>
            </html>";
    }
}
