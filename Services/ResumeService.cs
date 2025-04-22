using ResumeBuilderBackend.Data;
using ResumeBuilderBackend.Models;
using Microsoft.EntityFrameworkCore;
using ResumeBuilderBackend.DTOs;

namespace ResumeBuilderBackend.Services
{
    public class ResumeService
    {
        private readonly RegistrationDBcontext _context;

        public ResumeService(RegistrationDBcontext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ResumeDTO>> GetResumesByUserIdAsync(int userId)
        {
            return await _context.Resumes
                .Where(r => r.UserId == userId)
                .Select(r => new ResumeDTO
                {
                    ResumeId = r.ResumeId,
                    UserId = r.UserId,
                    TemplateId = r.TemplateId,
                    Name = r.Name,
                    Content = r.Content,
                    CreatedDate = r.CreatedDate,
                    ModifiedDate = r.ModifiedDate
                })
                .ToListAsync();
        }

        public async Task<ResumeDTO> GetResumeByIdAsync(int resumeId)
        {
            return await _context.Resumes
                .Where(r => r.ResumeId == resumeId)
                .Select(r => new ResumeDTO
                {
                    ResumeId = r.ResumeId,
                    UserId = r.UserId,
                    TemplateId = r.TemplateId,
                    Name = r.Name,
                    Content = r.Content,
                    CreatedDate = r.CreatedDate,
                    ModifiedDate = r.ModifiedDate
                })
                .FirstOrDefaultAsync();
        }

        public async Task<ResumeDTO> CreateResumeAsync(int userId, CreateResumeDTO createResumeDto)
        {
            if (createResumeDto == null)
                throw new ArgumentNullException(nameof(createResumeDto));

            if (string.IsNullOrWhiteSpace(createResumeDto.Name))
                throw new ArgumentException("Resume name is required", nameof(createResumeDto.Name));

            if (string.IsNullOrWhiteSpace(createResumeDto.Content))
                throw new ArgumentException("Resume content is required", nameof(createResumeDto.Content));

            if (userId <= 0)
                throw new ArgumentException("Invalid user ID", nameof(userId));

            var template = await _context.Templates.FindAsync(createResumeDto.TemplateId);
            if (template == null)
                throw new ArgumentException("Invalid template ID", nameof(createResumeDto.TemplateId));

            var user = await _context.Userdetails.FindAsync(userId);
            if (user == null)
                throw new ArgumentException("Invalid user ID", nameof(userId));

            var resume = new Resume
            {
                UserId = userId,
                TemplateId = createResumeDto.TemplateId,
                Name = createResumeDto.Name,
                Content = createResumeDto.Content, // Store raw content without sanitization
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _context.Resumes.Add(resume);
            await _context.SaveChangesAsync();

            return new ResumeDTO
            {
                ResumeId = resume.ResumeId,
                UserId = resume.UserId,
                TemplateId = resume.TemplateId,
                Name = resume.Name,
                Content = resume.Content,
                CreatedDate = resume.CreatedDate,
                ModifiedDate = resume.ModifiedDate
            };
        }

        public async Task<ResumeDTO> UpdateResumeAsync(int resumeId, CreateResumeDTO updateResumeDto)
        {
            if (updateResumeDto == null)
                throw new ArgumentNullException(nameof(updateResumeDto));

            var resume = await _context.Resumes.FindAsync(resumeId);
            if (resume == null)
                return null;

            if (!string.IsNullOrWhiteSpace(updateResumeDto.Name))
                resume.Name = updateResumeDto.Name;

            if (!string.IsNullOrWhiteSpace(updateResumeDto.Content))
                resume.Content = updateResumeDto.Content; // Store raw content without sanitization

            if (updateResumeDto.TemplateId > 0)
            {
                var template = await _context.Templates.FindAsync(updateResumeDto.TemplateId);
                if (template == null)
                    throw new ArgumentException("Invalid template ID", nameof(updateResumeDto.TemplateId));
                resume.TemplateId = updateResumeDto.TemplateId;
            }

            resume.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new ResumeDTO
            {
                ResumeId = resume.ResumeId,
                UserId = resume.UserId,
                TemplateId = resume.TemplateId,
                Name = resume.Name,
                Content = resume.Content,
                CreatedDate = resume.CreatedDate,
                ModifiedDate = resume.ModifiedDate
            };
        }

        public async Task<bool> DeleteResumeAsync(int resumeId)
        {
            var resume = await _context.Resumes.FindAsync(resumeId);
            if (resume == null)
                return false;

            _context.Resumes.Remove(resume);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}