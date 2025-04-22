using Microsoft.Extensions.Options;
using ResumeBuilderBackend.Models;
using System.Net.Mail;
using System.Net;
using MailKit.Security;
using MimeKit;

namespace ResumeBuilderBackend.Services
{
    /// <summary>
    ///  Service for sending emails using SMTP with support for both System.Net.Mail and MailKit implementations.
    /// Provides methods to send emails with HTML content or plain text, with robust configuration validation and error handling.
    /// </summary>
    public class EmailService : Services.IEmailservice
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailService"/> class.
        /// </summary>
        /// <param name="options">The email settings options containing SMTP configuration.</param>
        /// <param name="logger">The logger instance for logging email operations and errors.</param>
        /// <param name="configuration">The configuration instance for accessing email settings.</param>
        public EmailService(
            IOptions<EmailSettings> options,
            ILogger<EmailService> logger,
            IConfiguration configuration)
        {
            _emailSettings = options.Value;
            _logger = logger;
            _configuration = configuration;
        }
        /// <summary>
        /// Sends an email with HTML content using System.Net.Mail SMTP client.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public async Task SendEmail(MailRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "Mail request cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Recipient email address is required", nameof(request.Email));
            }

            try
            {
                // Get EmailSettings from configuration
                var emailSettings = _configuration.GetSection("EmailSettings");

                // Validate email settings
                ValidateEmailSettings(emailSettings);

                // Create SMTP Client for Gmail with more robust configuration
                using var client = new System.Net.Mail.SmtpClient(emailSettings["Host"], int.Parse(emailSettings["Port"]))
                {
                    Credentials = new NetworkCredential(
                        emailSettings["Email"],
                        emailSettings["Password"]
                    ),
                    EnableSsl = bool.Parse(emailSettings["EnableSsl"]),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 20000 // 20 seconds timeout
                };

                // Create Mail Message with additional security
                var message = new MailMessage
                {
                    From = new MailAddress(
                        emailSettings["Email"],
                        emailSettings["DisplayName"]
                    ),
                    Subject = request.Subject ?? "No Subject",
                    Body = request.Emailbody ?? string.Empty,
                    IsBodyHtml = true
                };
                message.To.Add(request.Email);

                // Log email sending attempt
                _logger.LogInformation($"Attempting to send email to {request.Email}");

                // Send Email with comprehensive error handling
                await client.SendMailAsync(message);

                // Logging successful email send
                _logger.LogInformation($"Email sent successfully to {request.Email}");
            }
            catch (SmtpException smtpEx)
            {
                // Specific SMTP exception handling
                _logger.LogError($"SMTP Error: {smtpEx.Message}");
                _logger.LogError($"SMTP Status Code: {smtpEx.StatusCode}");
                throw new ApplicationException("Failed to send email due to SMTP error", smtpEx);
            }
            catch (Exception ex)
            {
                // Comprehensive error logging
                _logger.LogError($"Email sending failed: {ex.Message}");
                _logger.LogError($"Exception Type: {ex.GetType().Name}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                throw new ApplicationException("Failed to send email", ex);
            }
        }

        /// <summary>
        /// Validates email settings before attempting to send email
        /// </summary>
        private void ValidateEmailSettings(IConfigurationSection emailSettings)
        {
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(emailSettings["Host"]))
                validationErrors.Add("SMTP Host is required");

            if (!int.TryParse(emailSettings["Port"], out _))
                validationErrors.Add("Invalid SMTP Port");

            if (string.IsNullOrWhiteSpace(emailSettings["Email"]))
                validationErrors.Add("Sender Email is required");

            if (string.IsNullOrWhiteSpace(emailSettings["Password"]))
                validationErrors.Add("Email Password is required");

            if (validationErrors.Any())
            {
                var errorMessage = string.Join("; ", validationErrors);
                _logger.LogError($"Email configuration invalid: {errorMessage}");
                throw new ConfigurationException($"Invalid email settings: {errorMessage}");
            }
        }
        /// <summary>
        /// Sends a plain-text email asynchronously using MailKit SMTP client.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="subject"></param>
        /// <param name="message"></param>
        /// <returns></returns>

        public async Task SendEmailAsync(string email, string subject, string message)

        {

            var emailMessage = new MimeMessage();

            emailMessage.From.Add(new MailboxAddress(_emailSettings.DisplayName, _emailSettings.Email));

            emailMessage.To.Add(new MailboxAddress("", email));

            emailMessage.Subject = subject;

            emailMessage.Body = new TextPart("plain") { Text = message };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();

            await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port, SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(_emailSettings.Email, _emailSettings.Password);

            await smtp.SendAsync(emailMessage);

            await smtp.DisconnectAsync(true);

        }

        /// <summary>
        /// Custom exception for configuration errors
        /// </summary>
        public class ConfigurationException : Exception
        {
            public ConfigurationException(string message) : base(message) { }
        }
    }
}