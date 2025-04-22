using ResumeBuilderBackend.DTOs; // Changed from static import
using Microsoft.AspNetCore.Mvc;
using ResumeBuilderBackend.Services;

namespace ResumeBuilderBackend.Controllers
{
    [Route("api/password")]
    [ApiController]
    public class PasswordResetController : ControllerBase
    {
        private readonly UserService _userService;

        public PasswordResetController(UserService userService)
        {
            _userService = userService;
        }

        // Step 1: Initiate password reset process
        [HttpPost("forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return BadRequest(new { success = false, message = "Email is required" });
            }

            try
            {
                // This will send OTP to the user's email
                string otp = await _userService.InitiatePasswordReset(request.Email);

                return Ok(new
                {
                    success = true,
                    message = "Password reset OTP sent successfully. Please check your email.",
                    otp = otp
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Password reset initiation failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, new { success = false, message = "Failed to initiate password reset", error = ex.Message });
            }
        }

        // Step 2: Verify OTP before allowing password reset - now only requires OTP
        [HttpPost("verify-otp")]
        public IActionResult VerifyResetOtp([FromBody] OtpOnlyDto request)
        {
            if (string.IsNullOrEmpty(request.Otp))
            {
                return BadRequest(new { success = false, message = "OTP is required" });
            }

            try
            {
                bool isVerified = _userService.VerifyPasswordResetOtpWithoutEmail(request.Otp);

                if (isVerified)
                {
                    string email = _userService.GetEmailByResetOtp(request.Otp);

                    return Ok(new
                    {
                        success = true,
                        message = "OTP verified successfully. Please set your new password."
                    });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Invalid or expired OTP" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OTP verification failed: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Verification failed", error = ex.Message });
            }
        }

        // Step 3: Reset password with email, new password and confirm password
        [HttpPatch("reset")]
        public IActionResult ResetPassword([FromBody] ResetPasswordWithEmailRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) ||
                string.IsNullOrEmpty(request.NewPassword) ||
                string.IsNullOrEmpty(request.ConfirmPassword))
            {
                return BadRequest(new { success = false, message = "All fields are required" });
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "Passwords do not match" });
            }

            try
            {
                bool isReset = _userService.ResetPassword(request.Email, request.NewPassword);

                if (isReset)
                {
                    return Ok(new { success = true, message = "Password reset successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to reset password. Please try again." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Password reset failed: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Password reset failed", error = ex.Message });
            }
        }

        // Optional: Resend OTP if needed
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendResetOtp([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return BadRequest(new { success = false, message = "Email is required" });
            }

            try
            {
                string otp = await _userService.ResendPasswordResetOtp(request.Email);
                return Ok(new
                {
                    success = true,
                    message = "OTP resent successfully",
                    otp = otp
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Resend OTP failed: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Failed to resend OTP", error = ex.Message });
            }
        }
    }
}