using ResumeBuilderBackend.Data;
using ResumeBuilderBackend.DTOs;
using ResumeBuilderBackend.Models;
using ResumeBuilderBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ResumeBuilderBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly RegistrationDBcontext _dbcontext;
        private readonly UserService _userService;
        private readonly EmailService _emailService;

        public AdminController(RegistrationDBcontext dbcontext, UserService userService,EmailService emailService)
        {
            _dbcontext = dbcontext;
            _userService = userService;
            _emailService = emailService;
        }

        [HttpGet("GetUserPagination")]
        public async Task<IActionResult> GetUsers(int pageNumber = 1, int pageSize = 10)
        {
            var usersQuery = _dbcontext.Userdetails
                                     .Where(u => u.role != "Admin") 
                                     .OrderBy(u => u.Id); 
            int totalUsers = await usersQuery.CountAsync();
            var users = await usersQuery
                                 .Skip((pageNumber - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync();
            return Ok(new { users, totalUsers });
        }


        [HttpGet("GetUsersByPage")]

        public async Task<IActionResult> GetUsersByPage(int pageNumber = 1, int pageSize = 10)

        {
            if (pageNumber < 1 || pageSize < 1)
            {

             return BadRequest("Invalid page number or page size.");

            }
            var query = _dbcontext.Userdetails.Where(u => u.role == "User");

            var totalUsers = await query.CountAsync();

            var users = await query

                .Skip((pageNumber - 1) * pageSize)

                .Take(pageSize)

                .ToListAsync();

            return Ok(new {Users = users});

        }

        [HttpGet("GetTemplatesByPage")]
        public async Task<IActionResult> GetTemplatesByPage(int pageNumber = 1, int pageSize = 10)
        {
            if (pageNumber < 1 || pageSize < 1)
            {
                return BadRequest("Invalid page number or page size.");
            }
            var totalTemplates = await _dbcontext.Templates.CountAsync();
            var totalPages = (int)Math.Ceiling(totalTemplates / (double)pageSize);
            if (pageNumber > totalPages && totalPages != 0)
            {
                return NotFound($"Page number exceeds total pages. Total pages: {totalPages}");
            }
            var templates = await _dbcontext.Templates
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return Ok(new
            {
                TotalTemplates = totalTemplates,
                TotalPages = totalPages,
                CurrentPage = pageNumber,
                Templates = templates
            });
        }


        //Adding new user
        [HttpPost("AddUser")]
        public async Task<IActionResult> AddUser([FromBody] UserRegistration user)
        {
            if (user == null)
            {
                return BadRequest("User data is required.");
            }

            // Check if user already exists by email
            var existingUser = await _dbcontext.Userdetails
                .FirstOrDefaultAsync(u => u.Email.ToLower() == user.Email.ToLower());

            if (existingUser != null)
            {
                return Conflict(new { message = "A user with this email already exists." });
            }

            string originalPassword = user.PasswordHash;

            string hashedPassword = HashPassword(user.PasswordHash);
            user.PasswordHash = hashedPassword;
            user.confirmPasswordHash = hashedPassword;

            _dbcontext.Userdetails.Add(user);
            await _dbcontext.SaveChangesAsync();

            try
            {
                await _userService.SendCredentialsEmail(user.Email, user.FirstName, user.Email, originalPassword);
                return CreatedAtAction(nameof(AddUser), new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                // Log the error but still return success since user was created
                Console.WriteLine($"Error sending credentials email: {ex.Message}");
                return CreatedAtAction(nameof(AddUser), new { id = user.Id },
                    new { user = user, message = "User created but email notification failed" });
            }
        }

        // Password hashing method
        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        // DELETE: api/user/{id}
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _dbcontext.Userdetails.FindAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            _dbcontext.Userdetails.Remove(user);
            await _dbcontext.SaveChangesAsync();
            return Ok("User deleted successfully.");
        }

        //Getting all managers
        [HttpGet("getallmanagers")]
        public async Task<ActionResult<IEnumerable<UserRegistration>>> GetManagers()
        {
            var managers = await _dbcontext.Userdetails
                                         .Where(u => u.role.ToLower() == "manager")
                                         .ToListAsync();
            if (!managers.Any())
            {
                return NotFound("No managers found.");
            }
            return Ok(managers);
        }

        //  POST: Add a new manager
        [HttpPost("Addmanager")]
        public async Task<IActionResult> AddManager([FromBody] UserRegistration user)
        {
            if (user == null)
            {
                return BadRequest("User data is required.");
            }

            // Check if user already exists by email
            var existingUser = await _dbcontext.Userdetails
                .FirstOrDefaultAsync(u => u.Email.ToLower() == user.Email.ToLower());

            if (existingUser != null)
            {
                return Conflict(new { message = "A user with this email already exists." });
            }

            // Store original password before hashing
            string originalPassword = user.PasswordHash;

            // Generate a single hash and assign it to both PasswordHash and ConfirmPasswordHash
            string hashedPassword = HashPassword(user.PasswordHash);
            user.PasswordHash = hashedPassword;
            user.confirmPasswordHash = hashedPassword;

            _dbcontext.Userdetails.Add(user);
            await _dbcontext.SaveChangesAsync();

            // Send credentials email to the user
            try
            {
                await _userService.SendCredentialsEmail(user.Email, user.FirstName, user.Email, originalPassword);
                return CreatedAtAction(nameof(AddUser), new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending credentials email: {ex.Message}");
                return CreatedAtAction(nameof(AddUser), new { id = user.Id },
                    new { user = user, message = "Manager created but email notification failed" });
            }
        }

        
        //  DELETE: Remove a manager
        [HttpDelete("manager/{id}")]
        public async Task<IActionResult> DeleteManager(int id)
        {
            var user = await _dbcontext.Userdetails.FindAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            _dbcontext.Userdetails.Remove(user);
            await _dbcontext.SaveChangesAsync();
            return Ok("User deleted successfully.");
        }


        [HttpPatch("ChangeRole/{id}")]
        public async Task<IActionResult> ChangeUserRole(int id, [FromBody] RoleUpdateDto roleUpdateDto)
        {
            var user = await _dbcontext.Userdetails.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }
            // Check if the user is currently a Manager
            if (user.role != "manager")
            {
                return BadRequest(new { message = "Only a Manager can be promoted to Admin." });
            }
            // Update role only if the current role is 'Manager'
            user.role = roleUpdateDto.Role;
            _dbcontext.Userdetails.Update(user);
            await _dbcontext.SaveChangesAsync();
            return Ok(new { message = "User role updated successfully", user });
        }

        [HttpPost("batch-insert")]
        public async Task<IActionResult> BatchInsertEmployees([FromBody] List<UserRegistrationDTO> Users)
        {
            if (Users == null || !Users.Any())
                return BadRequest("No employee data provided!");

            var existingEmails = await _dbcontext.Userdetails.Select(e => e.Email).ToListAsync();
            var newRegistration = new List<UserRegistration>();

            foreach (var userRegistrationDto in Users)
            {
                if (!existingEmails.Contains(userRegistrationDto.Email))
                {
                    var obj = new UserRegistration
                    {
                        FirstName = userRegistrationDto.FirstName,
                        LastName = userRegistrationDto.LastName,
                        Email = userRegistrationDto.Email

                    };
                    newRegistration.Add(obj);
                }
            }

            if (!newRegistration.Any())
                return BadRequest("No new employees to add.");

            _dbcontext.Userdetails.AddRange(newRegistration);
            await _dbcontext.SaveChangesAsync();

            // Send emails to new employees
            foreach (var sendmail in newRegistration)
            {
                var mailRequest = new MailRequest
                {
                    Email = sendmail.Email,
                    Subject = "Account Created - Change Your Password",
                    Emailbody = $"Dear {sendmail.FirstName},\n\nYour account has been created. Please change your password using the following link:\n\n[http://localhost:5294/api/password/forgot]\n\nBest regards,\nYour Company"
                };
                await _emailService.SendEmailAsync(mailRequest.Email, mailRequest.Subject, mailRequest.Emailbody);
            }

            return Ok("Employees added successfully!");
        }
    }
}
public class RoleUpdateDto

{
    public string Role { get; set; }

}