using System.Net;
using System.Net.Mail;
using System.Collections.Concurrent;
using ResumeBuilderBackend.Models;
using ResumeBuilderBackend.Data;

namespace ResumeBuilderBackend.Services
{
    /// <summary>
    /// Service for managing user registration, OTP verification, password reset, and credential emailing.
    /// Handles database operations, email sending, and secure password hashing.
    /// </summary>
    public class UserService
    {
        private readonly RegistrationDBcontext _context;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Stores pending user registrations with OTPs and expiry times.
        /// </summary>
        private static ConcurrentDictionary<string, PendingRegistration> _pendingRegistrations = new ConcurrentDictionary<string, PendingRegistration>();
        /// <summary>
        /// Stores current user registrations indexed by OTP for quick lookup.
        /// </summary>
        private static ConcurrentDictionary<string, UserRegistration> _currentUserRegistrations =
            new ConcurrentDictionary<string, UserRegistration>();
        /// <summary>
        /// Stores password reset requests with OTPs and expiry times
        /// </summary>
        private static ConcurrentDictionary<string, PasswordResetRequest> _passwordResetRequests = new ConcurrentDictionary<string, PasswordResetRequest>();
        /// <summary>
        /// The duration (in minutes) for which an OTP remains valid.
        /// </summary>
        private const int OTP_TIMEOUT_MINUTES = 10;
        /// <summary>
        /// Initializes a new instance of the <see cref="userService"/> class.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        public UserService(RegistrationDBcontext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        /// <summary>
        /// Generates a random 6-digit OTP for email verification or password reset.
        /// </summary>
        /// <returns></returns>
     
        private string GenerateNumber()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        /// <summary>
        /// Sends an OTP email to the specified user for verification or password reset purposes.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="otp"></param>
        /// <param name="name"></param>
        /// <param name="subject"></param>
        /// <param name="purpose"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        private async Task sendOtpMail(string email, string otp, string name, string subject = "Email Verification OTP", string purpose = "email verification")
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentNullException(nameof(email), "Email address cannot be null or empty");
            }

            // Get email settings from configuration - support both naming conventions from the files
            string fromEmail = _configuration["EmailSettings:Email"] ?? _configuration["EmailSettings:FromEmail"];
            string fromPassword = _configuration["EmailSettings:Password"];
            string smtpHost = _configuration["EmailSettings:Host"] ?? _configuration["EmailSettings:SmtpHost"];
            string smtpPortStr = _configuration["EmailSettings:Port"] ?? _configuration["EmailSettings:SmtpPort"];

            // Validate email settings
            if (string.IsNullOrEmpty(fromEmail))
            {
                throw new InvalidOperationException("Sender email address not configured in appsettings.json");
            }

            if (string.IsNullOrEmpty(fromPassword))
            {
                throw new InvalidOperationException("Sender email password not configured in appsettings.json");
            }

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpPortStr))
            {
                throw new InvalidOperationException("SMTP settings not properly configured in appsettings.json");
            }

            try
            {
                int smtpPort = int.Parse(smtpPortStr);
                var fromAddress = new MailAddress(fromEmail, "Q-Resume Builder");
                var toAddress = new MailAddress(email);

                //string body = $"<h1>{subject}</h1><p>Hello {name},</p><p>Your OTP for {purpose} is: <strong>{otp}</strong></p><p>This OTP is valid for {OTP_TIMEOUT_MINUTES} minutes.</p>";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 5px;'>
                    <h1 style='color: #2c3e50; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>{subject}</h1>
                    <p style='font-size: 16px; color: #333;'>Hello <span style='font-weight: bold;'>{name}</span>,</p>
                    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 4px; text-align: center; margin: 20px 0;'>
                        <p style='font-size: 16px; margin-bottom: 10px;'>Your OTP for {purpose} is:</p>
                        <p style='font-size: 24px; font-weight: bold; letter-spacing: 2px; color: #3498db; margin: 0;'>{otp}</p>
                    </div>
                    <p style='font-size: 14px; color: #777; text-align: center;'>This OTP is valid for {OTP_TIMEOUT_MINUTES} minutes.</p>
                    <div style='font-size: 12px; text-align: center; margin-top: 20px; color: #999; border-top: 1px solid #eee; padding-top: 10px;'>
                        This is an automated message. Please do not reply to this email.
                    </div>
                </div>";

                //string body = $"Hello {name},\n\nYour OTP is: {otp}\n\nThis OTP will be valid for {OTP_TIMEOUT_MINUTES} minutes.\n\nThank you,\nQ-Resume Builder Team";




                var smtp = new SmtpClient
                {
                    Host = smtpHost,
                    Port = smtpPort,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send email: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// Initiates user registration by validating user data, hashing passwords, generating an OTP, and sending it via email.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> InitiateRegistration(UserRegistration user)
        {
            // Hash password (from first file)
            string originalPassword = user.PasswordHash;
            user.PasswordHash = HashPassword(originalPassword);

            // If confirmPasswordHash is the same as the original password, hash it too
            if (user.confirmPasswordHash == originalPassword)
            {
                user.confirmPasswordHash = user.PasswordHash;
            }
            else if (!string.IsNullOrEmpty(user.confirmPasswordHash))
            {
                user.confirmPasswordHash = HashPassword(user.confirmPasswordHash);
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "User data cannot be null");
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                throw new ArgumentException("Email is required", nameof(user.Email));
            }

            if (string.IsNullOrEmpty(user.FirstName))
            {
                throw new ArgumentException("First name is required", nameof(user.FirstName));
            }

            // Check if email already exists in the database
            if (_context.Userdetails.Any(u => u.Email == user.Email))
            {
                throw new InvalidOperationException("Email already registered");
            }

            // Ensure required fields have values
            if (string.IsNullOrEmpty(user.role))
            {
                user.role = "User";
            }

            // Make sure confirmPasswordHash matches PasswordHash if not provided
            if (string.IsNullOrEmpty(user.confirmPasswordHash))
            {
                user.confirmPasswordHash = user.PasswordHash;
            }

            // Generate OTP
            string otp = GenerateNumber();

            // Store user data temporarily with OTP
            var pendingUser = new PendingRegistration
            {
                UserData = user,
                Otp = otp,
                ExpiryTime = DateTime.UtcNow.AddMinutes(OTP_TIMEOUT_MINUTES)
            };

            _pendingRegistrations[user.Email] = pendingUser;

            // Also store in our current registrations dictionary
            _currentUserRegistrations[otp] = user;

            // Send OTP email
            await sendOtpMail(user.Email, otp, user.FirstName);

            return otp; // Return OTP (remove in production)
        }

        /// <summary>
        /// Retrieves user registration data associated with a given OTP.
        /// </summary>
        /// <param name="otp"></param>
        /// <returns></returns>
        public UserRegistration GetUserByOtp(string otp)
        {
            if (_currentUserRegistrations.TryGetValue(otp, out var user))
            {
                return user;
            }
            return null;
        }
        /// <summary>
        /// Verifies an OTP and completes user registration if valid.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="otp"></param>
        /// <returns></returns>
        public bool VerifyOtpAndRegister(string email, string otp)
        {
            if (string.IsNullOrEmpty(otp))
            {
                return false;
            }

            // If email is not provided, try to get it from the current registrations
            if (string.IsNullOrEmpty(email) && _currentUserRegistrations.TryGetValue(otp, out var user))
            {
                email = user.Email;
            }

            if (string.IsNullOrEmpty(email))
            {
                return false;
            }

            // Check if there's a pending registration with this email
            if (!_pendingRegistrations.TryGetValue(email, out var pendingReg))
            {
                return false; // No pending registration found
            }

            // Check if OTP is valid and not expired
            if (pendingReg.Otp != otp || DateTime.UtcNow > pendingReg.ExpiryTime)
            {
                return false; // Invalid OTP or expired
            }

            try
            {
                // OTP is valid, register the user
                var userData = pendingReg.UserData;

                // Make sure required fields have values
                if (string.IsNullOrEmpty(userData.role))
                {
                    userData.role = "User";
                }

                if (string.IsNullOrEmpty(userData.LastName))
                {
                    userData.LastName = ""; // Provide empty string if LastName is null
                }

                // Make sure confirmPasswordHash matches PasswordHash if not provided
                if (string.IsNullOrEmpty(userData.confirmPasswordHash))
                {
                    userData.confirmPasswordHash = userData.PasswordHash;
                }

                _context.Userdetails.Add(userData);
                int result = _context.SaveChanges();

                _pendingRegistrations.TryRemove(email, out _);
                _currentUserRegistrations.TryRemove(otp, out _);

                return result > 0; // Return true if at least one record was saved
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                return false; 
            }
        }
        /// <summary>
        /// Resends an OTP for a pending user registration.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> ResendOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
                throw new ArgumentException("Email is required", nameof(email));
            

            if (!_pendingRegistrations.TryGetValue(email, out var pendingReg)) 
               throw new InvalidOperationException("No pending registration found for this email");

            var oldOtp = pendingReg.Otp;

            _currentUserRegistrations.TryRemove(oldOtp, out _);

           
            string newOtp = GenerateNumber();

            

            pendingReg.Otp = newOtp;

            pendingReg.ExpiryTime = DateTime.UtcNow.AddMinutes(OTP_TIMEOUT_MINUTES);

            _pendingRegistrations[email] = pendingReg;

            // Add the new OTP to currentUserRegistrations

            _currentUserRegistrations[newOtp] = pendingReg.UserData;

            // Send email with new OTP

            await sendOtpMail(email, newOtp, pendingReg.UserData.FirstName);

            return newOtp; 

        }
        /// <summary>
        /// Verifies an OTP and completes user registration using only the OTP.
        /// </summary>
        /// <param name="otp"></param>
        /// <returns></returns>

        public bool VerifyOtp(string otp)
        {
            if (string.IsNullOrEmpty(otp))
            {
                return false;
            }

            // Get the user associated with this OTP
            if (!_currentUserRegistrations.TryGetValue(otp, out var user))
            {
                return false; // No user found for this OTP
            }

            string email = user.Email;

            // Now that we have the email, use the existing method
            return VerifyOtpAndRegister(email, otp);
        }

        /// <summary>
        /// Hashes a password using BCrypt for secure storage.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        private string HashPassword(string password)
        {
            // Use BCrypt for password hashing (secure and industry standard)
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        /// <summary>
        /// Initiates a password reset process by generating an OTP and sending it via email.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> InitiatePasswordReset(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("Email is required", nameof(email));
            }

            // Check if email exists in the database
            var user = _context.Userdetails.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                throw new InvalidOperationException("Email not registered");
            }

            // Generate OTP
            string otp = GenerateNumber();

            // Store password reset request data
            var resetRequest = new PasswordResetRequest
            {
                Email = email,
                Otp = otp,
                ExpiryTime = DateTime.UtcNow.AddMinutes(OTP_TIMEOUT_MINUTES)
            };

            _passwordResetRequests[email] = resetRequest;

            // Also store OTP as a key for quick lookup
            _passwordResetRequests[otp] = resetRequest;

            // Send OTP email
            await sendOtpMail(
                email,
                otp,
                user.FirstName,
                "Password Reset Request",
                "password reset"
            );

            return otp; // Return OTP (remove in production)
        }

        /// <summary>
        /// Verifies a password reset OTP for a given email
        /// </summary>
        /// <param name="email"></param>
        /// <param name="otp"></param>
        /// <returns></returns>
        public bool VerifyPasswordResetOtp(string email, string otp)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
            {
                return false;
            }

            // Check if there's a password reset request with this email
            if (!_passwordResetRequests.TryGetValue(email, out var resetRequest))
            {
                return false; // No password reset request found
            }

            // Check if OTP is valid and not expired
            if (resetRequest.Otp != otp || DateTime.UtcNow > resetRequest.ExpiryTime)
            {
                return false; // Invalid OTP or expired
            }

            return true;
        }

        /// <summary>
        /// Verifies a password reset OTP without requiring an email address.
        /// </summary>
        /// <param name="otp"></param>
        /// <returns></returns>
        public bool VerifyPasswordResetOtpWithoutEmail(string otp)
        {
            if (string.IsNullOrEmpty(otp))
            {
                return false;
            }

            // Check if there's a password reset request with this OTP
            if (!_passwordResetRequests.TryGetValue(otp, out var resetRequest))
            {
                return false; // No password reset request found
            }

            // Check if not expired
            if (DateTime.UtcNow > resetRequest.ExpiryTime)
            {
                return false; // Expired
            }

            return true;
        }

        /// <summary>
        /// Retrieves the email address associated with a password reset OTP.
        /// </summary>
        /// <param name="otp"></param>
        /// <returns></returns>
        public string GetEmailByResetOtp(string otp)
        {
            if (string.IsNullOrEmpty(otp))
            {
                return null;
            }

            // Check if there's a password reset request with this OTP
            if (_passwordResetRequests.TryGetValue(otp, out var resetRequest))
            {
                // Ensure it's not expired
                if (DateTime.UtcNow <= resetRequest.ExpiryTime)
                {
                    return resetRequest.Email;
                }
            }

            return null;
        }
        /// <summary>
        /// Resets a user's password for the specified email.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        public bool ResetPassword(string email, string newPassword)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(newPassword))
            {
                return false;
            }

            try
            {
                // Find user by email
                var user = _context.Userdetails.FirstOrDefault(u => u.Email == email);
                if (user == null)
                {
                    return false; // User not found
                }

                // Hash the new password before storing
                string hashedPassword = HashPassword(newPassword);

                // Update password
                user.PasswordHash = hashedPassword;
                user.confirmPasswordHash = hashedPassword;
                _context.Userdetails.Update(user);
                int result = _context.SaveChanges();

                // If there's a password reset request for this email, clear it
                if (_passwordResetRequests.TryGetValue(email, out var resetRequest))
                {
                    _passwordResetRequests.TryRemove(email, out _);
                    _passwordResetRequests.TryRemove(resetRequest.Otp, out _);
                }

                return result > 0; // Return true if password was updated
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error resetting password: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return false; // Database error
            }
        }

        /// <summary>
        /// Resets a user's password using an OTP.
        /// </summary>
        /// <param name="otp"></param>
        /// <param name="newPassword"></param>
        /// <returns></returns>
        public bool ResetPasswordWithOtp(string otp, string newPassword)
        {
            if (string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(newPassword))
            {
                return false;
            }

            // Get the email associated with this OTP
            string email = GetEmailByResetOtp(otp);
            if (string.IsNullOrEmpty(email))
            {
                return false; // OTP not found or expired
            }

            // Now reset the password using the email
            return ResetPassword(email, newPassword);
        }

        /// <summary>
        /// Resends a password reset OTP for a registered user.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> ResendPasswordResetOtp(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentException("Email is required", nameof(email));
            }

            // Get user information
            var user = _context.Userdetails.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                throw new InvalidOperationException("Email not registered");
            }

            // Check if there's an existing password reset request
            if (_passwordResetRequests.TryGetValue(email, out var existingRequest))
            {
                // Remove existing OTP key
                _passwordResetRequests.TryRemove(existingRequest.Otp, out _);
            }

            // Generate new OTP
            string newOtp = GenerateNumber();

            // Create or update password reset request
            var resetRequest = new PasswordResetRequest
            {
                Email = email,
                Otp = newOtp,
                ExpiryTime = DateTime.UtcNow.AddMinutes(OTP_TIMEOUT_MINUTES)
            };

            _passwordResetRequests[email] = resetRequest;
            _passwordResetRequests[newOtp] = resetRequest;

            // Send email with new OTP
            await sendOtpMail(
                email,
                newOtp,
                user.FirstName,
                "Password Reset Request",
                "password reset"
            );

            return newOtp; // Remove in production
        }

        /// <summary>
        /// Sends an email containing account credentials to a user.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="name"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public async Task SendCredentialsEmail(string email, string name, string username, string password)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentNullException(nameof(email), "Email address cannot be null or empty");
            }

            string subject = "Your Account Credentials";
            string purpose = "account access";

            // Get email settings from configuration
            string fromEmail = _configuration["EmailSettings:Email"] ?? _configuration["EmailSettings:FromEmail"];
            string fromPassword = _configuration["EmailSettings:Password"];
            string smtpHost = _configuration["EmailSettings:Host"] ?? _configuration["EmailSettings:SmtpHost"];
            string smtpPortStr = _configuration["EmailSettings:Port"] ?? _configuration["EmailSettings:SmtpPort"];

            // Validate email settings
            if (string.IsNullOrEmpty(fromEmail))
            {
                throw new InvalidOperationException("Sender email address not configured in appsettings.json");
            }

            if (string.IsNullOrEmpty(fromPassword))
            {
                throw new InvalidOperationException("Sender email password not configured in appsettings.json");
            }

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpPortStr))
            {
                throw new InvalidOperationException("SMTP settings not properly configured in appsettings.json");
            }

            try
            {
                int smtpPort = int.Parse(smtpPortStr);
                var fromAddress = new MailAddress(fromEmail, "Q-Resume Builder");
                var toAddress = new MailAddress(email);

                string body = $@"
    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 5px;'>
    <h1 style='color: #2c3e50; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>{subject}</h1>
    <p style='font-size: 16px; color: #333;'>Hello <span style='font-weight: bold;'>{name}</span>,</p>
    <p style='font-size: 16px; color: #333;'>An administrator has created an account for you. Here are your login credentials:</p>
    <div style='background-color: #f8f9fa; padding: 15px; border-radius: 4px; margin: 20px 0;'>
        <p style='font-size: 16px; margin-bottom: 10px;'><strong>Username/Email:</strong> {username}</p>
        <p style='font-size: 16px; margin-bottom: 10px;'><strong>Password:</strong> {password}</p>
    </div>
    <p style='font-size: 14px; color: #777;'>Please keep this information secure. We recommend changing your password after your first login.</p>
    <div style='font-size: 12px; text-align: center; margin-top: 20px; color: #999; border-top: 1px solid #eee; padding-top: 10px;'>
        This is an automated message. Please do not reply to this email.
    </div>
</div>";

                var smtp = new SmtpClient
                {
                    Host = smtpHost,
                    Port = smtpPort,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    await smtp.SendMailAsync(message);
                }
            }
            catch (Exception ex)
            {
                // Add more detailed error information
                throw new Exception($"Failed to send credentials email: {ex.Message}", ex);
            }
        }

        
       
    }

    /// <summary>
    /// Represents a pending user registration with associated OTP and expiry time.
    /// </summary>
    public class PendingRegistration
    {
        public UserRegistration UserData { get; set; }
        public string Otp { get; set; }
        public DateTime ExpiryTime { get; set; }
    }

    /// <summary>
    /// Represents a password reset request with associated email, OTP, and expiry time.
    /// </summary>
    public class PasswordResetRequest
    {
        public string Email { get; set; }
        public string Otp { get; set; }
        public DateTime ExpiryTime { get; set; }
    }
}