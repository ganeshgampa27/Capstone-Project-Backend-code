using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeBuilderBackend.Data;
using ResumeBuilderBackend.DTOs;
using ResumeBuilderBackend.Services;

namespace ResumeBuilderBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResumesController : ControllerBase
    {
        private readonly ResumeService _resumeService;
        private readonly ILogger<ResumesController> _logger;
        private readonly RegistrationDBcontext _dbcontext;

        public ResumesController(ResumeService resumeService, ILogger<ResumesController> logger , RegistrationDBcontext dbcontext)
        {
            _resumeService = resumeService;
            _logger = logger;
            _dbcontext= dbcontext;
        }

        // ResumeController.cs

        [HttpGet("GetUserResumes")]

        public async Task<IActionResult> GetUserResumes(int userId, int page = 1, int pageSize = 8)
        {

            var query = _dbcontext.Resumes

                .Where(r => r.UserId == userId)

                .OrderByDescending(r => r.CreatedDate); // Assuming there's a CreatedDate column

            var totalResumes = await query.CountAsync();

            var resumes = await query

                .Skip((page - 1) * pageSize)

                .Take(pageSize)

                .ToListAsync();

            return Ok(new

            {

                totalCount = totalResumes,

                currentPage = page,

                pageSize = pageSize,

                totalPages = (int)Math.Ceiling((double)totalResumes / pageSize),

                data = resumes

            });

        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetResumeById(int id)
        {
            try
            {
                var resume = await _resumeService.GetResumeByIdAsync(id);
                if (resume == null)
                    return NotFound(new { message = $"Resume with ID {id} not found" });
                return Ok(resume);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting resume with ID {ResumeId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the resume" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateResume([FromBody] CreateResumeDTO createResumeDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var createdResume = await _resumeService.CreateResumeAsync(createResumeDto.UserId, createResumeDto);
                _logger.LogInformation("Resume created: {ResumeId}", createdResume.ResumeId);
                return CreatedAtAction(
                    nameof(GetResumeById),
                    new { id = createdResume.ResumeId },
                    createdResume);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid resume data");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating resume");
                return StatusCode(500, new { message = "An error occurred while creating the resume" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateResume(int id, [FromBody] CreateResumeDTO updateResumeDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var updatedResume = await _resumeService.UpdateResumeAsync(id, updateResumeDto);
                if (updatedResume == null)
                    return NotFound(new { message = "Resume with ID { id}not found" });
                _logger.LogInformation("Resume updated: {ResumeId}", id);
                return Ok(updatedResume);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid resume data for update");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating resume {ResumeId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the resume" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResume(int id)
        {
            try
            {
                var resume = await _resumeService.GetResumeByIdAsync(id);
                if (resume == null)
                    return NotFound(new { message = $"Resume with ID {id} not found" });
                var result = await _resumeService.DeleteResumeAsync(id);
                if (!result)
                    return BadRequest(new { message = "Resume could not be deleted" });
                _logger.LogInformation("Resume deleted: {ResumeId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting resume {ResumeId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the resume" });
            }
        }
    }
}