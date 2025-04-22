using ResumeBuilderBackend.Models;
using ResumeBuilderBackend.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ResumeBuilderBackend.Data;
using ResumeBuilderBackend.Services;

namespace ResumeBuilderBackend.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly RegistrationDBcontext _context;
        private readonly IConfiguration _configuration;
        private readonly UserService _userService;

        public AuthController(RegistrationDBcontext context, IConfiguration configuration, UserService userService)
        {
            _context = context;
            _configuration = configuration;
            _userService = userService;
        }

        // Step 1: Initiate Registration (Assign Role & Send OTP)

        [HttpPost("register/initiate")]

        public async Task<IActionResult> InitiateRegistration([FromBody] UserRegistration user)

        {

            if (string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.FirstName) || string.IsNullOrEmpty(user.PasswordHash))

            {

                return BadRequest(new { message = "Required fields are missing" });

            }

            try

            {

                // Count existing users in the database to determine the role

                int userCount = await _context.Userdetails.CountAsync();

                // Extract domain from email

                string emailDomain = user.Email.Split('@').Last().ToLower();

                string allowedDomain = "quadranttechnologies.com";

                if (userCount == 0) 

                {

                    if (emailDomain != allowedDomain)

                    {

                        return BadRequest(new { message = "Admin must belongs to Quadrant Organization." });

                    }

                    user.role = "admin";  // First user should be Admin

                }

                else if (userCount == 1) // Second user should be Manager

                {

                    if (emailDomain != allowedDomain)

                    {

                        return BadRequest(new { message = "Manager must belongs to Quadrant Organization." });

                    }

                    user.role = "manager";

                }

                else // All other users will be assigned as "User"

                {

                    user.role = "User";

                }

                // Ensure confirmPasswordHash has a value

                if (string.IsNullOrEmpty(user.confirmPasswordHash))

                {

                    user.confirmPasswordHash = user.PasswordHash;

                }

                

                string otp = await _userService.InitiateRegistration(user);

                return Ok(new

                {

                    message = "OTP sent successfully. Please verify your email to complete registration.",
                    otp = otp

                });

            }

            catch (Exception ex)

            {

                return StatusCode(500, new { message = "An error occurred during registration.", error = ex.Message });

            }

        }


        // Step 2: Verify OTP and complete registration - using only the OTP
        [HttpPost("register/verify")]
        public IActionResult VerifyRegistration([FromBody] OtpOnlyDto request)
        {
            if (string.IsNullOrEmpty(request.Otp))
            {
                return BadRequest(new { message = "OTP is required" });
            }

            try
            {
                bool isVerified = _userService.VerifyOtp(request.Otp);

                if (isVerified)
                {
                    return Ok(new { message = "Email verified and registration completed successfully" });
                }
                else
                {
                    return BadRequest(new { message = "Invalid or expired OTP" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verification failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, new { message = "Verification failed", error = ex.Message });
            }
        }

        // Optional: Resend OTP if needed
        [HttpPost("register/resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] EmailOnlyDto request)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            try
            {
                string otp = await _userService.ResendOtp(request.Email);
                return Ok(new
                {
                    message = "OTP resent successfully",
                    otp = otp
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to resend OTP", error = ex.Message });
            }
        }

        // Keep the existing login endpoint - Enhanced to support both authentication methods
        [HttpPost("Userlogin")]
        public IActionResult Login([FromBody] UserLoginRequest request)
        {
            var user = _context.Userdetails.SingleOrDefault(u => u.Email == request.Email);

            if (user != null)
            {
                if (BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    var token = GenerateJwtToken(user);
                    return Ok(new { message = "Login successful", token, user = user.role });
                }
                else if (user.PasswordHash == request.Password)
                {
                    var token = GenerateJwtToken(user);
                    return Ok(new { message = "Login successful", token, user = user.role });
                }
            }

            return Unauthorized(new { message = "Invalid credentials" });
        }

        // Consolidated JWT token generation method
        private string GenerateJwtToken(UserRegistration user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("userId", user.Id.ToString())
            };

            if (!string.IsNullOrEmpty(user.role))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}