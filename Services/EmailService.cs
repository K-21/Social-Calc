using MimeKit;
using MailKit.Net.Smtp;

namespace SocialCalc.Web.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    private readonly IWebHostEnvironment _env;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IWebHostEnvironment env)
    {
        _configuration = configuration;
        _logger = logger;
        _env = env;
    }

    private (string server, int port, string from) GetSmtpConfig()
    {
        var smtpServer = _configuration["Email:SmtpServer"] ?? "localhost";
        var smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out int p) ? p : 25;
        var fromEmail = _configuration["Email:From"] ?? "noreply@socialcalc.local";
        return (smtpServer, smtpPort, fromEmail);
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
    {
        try
        {
            var config = GetSmtpConfig();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Social Calc", config.from));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Password Reset Request";

            var builder = new BodyBuilder
            {
                HtmlBody = GeneratePasswordResetEmailBody(resetLink)
            };
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await ConnectAndAuthenticateAsync(client, config.server, config.port);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            _logger.LogInformation("Password reset email sent.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email");
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string email, string userName)
    {
        try
        {
            var config = GetSmtpConfig();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Social Calc", config.from));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Welcome to Social Calc";

            var builder = new BodyBuilder
            {
                HtmlBody = GenerateWelcomeEmailBody(userName)
            };
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await ConnectAndAuthenticateAsync(client, config.server, config.port);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            _logger.LogInformation("Welcome email sent.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email");
            return false;
        }
    }

    public async Task<bool> SendSheetSharedNotificationAsync(string email, string sheetName)
    {
        try
        {
            var config = GetSmtpConfig();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Social Calc", config.from));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Sheet Shared with You";

            var builder = new BodyBuilder
            {
                HtmlBody = GenerateSheetSharedEmailBody(sheetName)
            };
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await ConnectAndAuthenticateAsync(client, config.server, config.port);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            _logger.LogInformation("Sheet shared notification sent.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending sheet notification email");
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
        var publicHostname = _configuration["AppSettings:PublicHostname"] ?? "social-calc.duckdns.org";
        var scheme = _env.IsDevelopment() ? "http" : "https";
        return $@"
            <html>
            <body>
                <h2>Sheet Shared with You</h2>
                <p>A spreadsheet named '{sheetName}' has been shared with you.</p>
                <p><a href='{scheme}://{publicHostname}/sheets'>View your sheets</a></p>
            </body>
            </html>";
    }

    private async Task ConnectAndAuthenticateAsync(SmtpClient client, string smtpServer, int smtpPort)
    {
        if (_env.IsDevelopment())
        {
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }

        await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.Auto);

        var useCredentials = _configuration.GetValue<bool>("Email:UseCredentials", false);
        if (useCredentials)
        {
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
            }
        }
    }
}
