using ResumeBuilderBackend.Data;
using ResumeBuilderBackend.DTOs;
using ResumeBuilderBackend.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ResumeBuilderBackend.Services
{
    /// <summary>
    /// Service for managing templates, including creation, retrieval, updating, and deletion.
    /// Validates template content as HTML or JSON and handles database operations.
    /// </summary>
    public class TemplateService
    {
        private readonly RegistrationDBcontext _context;
        /// <summary>
        ///Initializes a new instance of the <see cref="TemplateService"/> class.
        /// </summary>
        /// <param name="context"></param>
        public TemplateService(RegistrationDBcontext context)
        {
            _context = context;
        }
        /// <summary>
        /// Retrieves all templates from the database.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Template>> GetAllTemplatesAsync()
        {
            var templates = await _context.Templates
                .Select(t => new Template
                {
                    Id = t.Id,
                    Name = t.Name,
                    Content = t.Content,
                    CreatedDate = t.CreatedDate
                })
                .ToListAsync();
            return templates;
        }
        /// <summary>
        /// Retrieves a specific template by its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Template> GetTemplateByIdAsync(int id)
        {
            var template = await _context.Templates
                .Where(t => t.Id == id)
                .Select(t => new Template
                {
                    Id = t.Id,
                    Name = t.Name,
                    Content = t.Content,
                    CreatedDate = t.CreatedDate
                })
                .FirstOrDefaultAsync();

            return template;
        }
        /// <summary>
        /// Creates a new template with the specified details.
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public async Task<Template> CreateTemplateAsync(Template template)
        {
            // Validate inputs
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (string.IsNullOrWhiteSpace(template.Name))
                throw new ArgumentException("Template name is required", nameof(template.Name));

            if (string.IsNullOrWhiteSpace(template.Content))
                throw new ArgumentException("Template content is required", nameof(template.Content));

            if (!IsValidContent(template.Content))
                throw new ArgumentException("Template content must be valid HTML or JSON", nameof(template.Content));

            // Create new template entity
            var temp = new Template
            {
                Name = template.Name,
                Content = template.Content,
                ContentType = template.ContentType, // Preserve ContentType from input
                CreatedDate = DateTime.UtcNow
            };

            // Add to database
            _context.Templates.Add(temp);
            await _context.SaveChangesAsync();

            // Return the saved entity with the updated Id
            return temp;
        }
        /// <summary>
        /// Updates an existing template with new details.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="templateDto"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public async Task<Template> UpdateTemplateAsync(int id, UpdateTemplateDTO templateDto)
        {
            if (templateDto == null)
                throw new ArgumentNullException(nameof(templateDto));

            var template = await _context.Templates.FindAsync(id);

            if (template == null)
                return null;

            // Update properties only if they're provided
            if (!string.IsNullOrWhiteSpace(templateDto.Name))
                template.Name = templateDto.Name;

            if (!string.IsNullOrWhiteSpace(templateDto.Content))
            {
                // Validate content format (optional)
                if (!IsValidContent(templateDto.Content))
                    throw new ArgumentException("Template content must be valid HTML or JSON", nameof(templateDto.Content));

                template.Content = templateDto.Content;
            }

            template.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new Template
            {
                Id = template.Id,
                Name = template.Name,
                Content = template.Content,
                CreatedDate = template.CreatedDate
            };
        }
        /// <summary>
        /// Deletes a template by its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<bool> DeleteTemplateAsync(int id)
        {
            var template = await _context.Templates.FindAsync(id);

            if (template == null)
                return false;

            // Check if template is assigned to any users
            //var hasAssignments = await _context.Templates
            //.AnyAsync(ut => ut.Id == id);

            //if (hasAssignments)
            //    return false;

            _context.Templates.Remove(template);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Validates whether the provided content is valid JSON or HTML.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>

        private bool IsValidContent(string content)
        {
            // Check if it's valid JSON
            try
            {
                JsonDocument.Parse(content);
                return true;
            }
            catch (JsonException)
            {
                // Not valid JSON, could be HTML
                // Add basic HTML validation if needed
                return content.Contains("<") && content.Contains(">");
            }
        }
    }
}
