using ResumeBuilderBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace ResumeBuilderBackend.Data
{
    public class RegistrationDBcontext :  DbContext
    {
        public RegistrationDBcontext(DbContextOptions<RegistrationDBcontext> options) : base(options) { }
        public DbSet<UserRegistration> Userdetails { get; set; }
        public DbSet<Template> Templates { get; set; }

        public DbSet<Resume> Resumes { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.Entity<Resume>().HasOne(r => r.User).WithMany(u => u.Resumes).HasForeignKey(r => r.UserId); modelBuilder.Entity<Resume>().HasOne(r => r.Template).WithMany().HasForeignKey(r => r.TemplateId); }


    }
}
