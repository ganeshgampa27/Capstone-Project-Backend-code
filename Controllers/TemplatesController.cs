using ResumeBuilderBackend.DTOs;
using ResumeBuilderBackend.Models;
using Microsoft.AspNetCore.Mvc;
using ResumeBuilderBackend.Services;

namespace ResumeBuilderBackend.Controllers
{ 
    [Route("api/[controller]")]

    [ApiController]

    public class TemplatesController : ControllerBase
    {
        private readonly TemplateService _templateService;
        private readonly ILogger<TemplatesController> _logger;
        public TemplatesController(TemplateService templateService, ILogger<TemplatesController> logger)
        {

            _templateService = templateService;
            _logger = logger;

        }

        [HttpGet]

        public async Task<IActionResult> GetAllTemplates()

        {

            try

            {

                var templates = await _templateService.GetAllTemplatesAsync();

                return Ok(templates);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error getting all templates");

                return StatusCode(500, "An error occurred while retrieving templates");

            }

        }

        [HttpGet("{id}")]

        public async Task<IActionResult> GetTemplateById(int id)

        {

            try

            {

                var template = await _templateService.GetTemplateByIdAsync(id);

                if (template == null)

                    return NotFound($"Template with ID {id} not found");

                return Ok(template);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error getting template with ID {TemplateId}", id);

                return StatusCode(500, "An error occurred while retrieving the template");

            }

        }

        [HttpPost]

        public async Task<IActionResult> CreateTemplate([FromBody] Template template)

        {

            if (!ModelState.IsValid)

                return BadRequest(ModelState);

            try

            {

                var createdTemplate = await _templateService.CreateTemplateAsync(template);

                _logger.LogInformation("Template created: {TemplateId}", createdTemplate.Id);

                return CreatedAtAction(

                    nameof(GetTemplateById),

                    new { id = createdTemplate.Id },

                    createdTemplate);

            }

            catch (ArgumentException ex)

            {

                _logger.LogWarning(ex, "Invalid template data");

                return BadRequest(ex.Message);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error creating template");

                return StatusCode(500, "An error occurred while creating the template");

            }

        }

        [HttpPut("{id}")]

        public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpdateTemplateDTO templateDto)

        {

            if (!ModelState.IsValid)

                return BadRequest(ModelState);

            try

            {

                var updatedTemplate = await _templateService.UpdateTemplateAsync(id, templateDto);

                if (updatedTemplate == null)

                    return NotFound($"Template with ID {id} not found");

                _logger.LogInformation("Template updated: {TemplateId}", id);

                return Ok(updatedTemplate);

            }

            catch (ArgumentException ex)

            {

                _logger.LogWarning(ex, "Invalid template data for update");

                return BadRequest(ex.Message);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error updating template {TemplateId}", id);

                return StatusCode(500, "An error occurred while updating the template");

            }

        }

        [HttpDelete("{id}")]

        public async Task<IActionResult> DeleteTemplate(int id)

        {

            try

            {

                var result = await _templateService.DeleteTemplateAsync(id);

                if (!result)

                    return BadRequest("Template could not be deleted. It may be assigned to users or not exist.");

                _logger.LogInformation("Template deleted: {TemplateId}", id);

                return NoContent();

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error deleting template {TemplateId}", id);

                return StatusCode(500, "An error occurred while deleting the template");

            }

        }

    }
}

