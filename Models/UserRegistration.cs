using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ResumeBuilderBackend.Models
{   
    public class UserRegistration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string confirmPasswordHash { get; set; } = string.Empty;
        public string role { get; set; } = "User";

        // Automatically store only the Date (without time)
        public DateTime JoinDate { get; set; } = DateTime.UtcNow.Date;

        [JsonIgnore]
      
        public List<Resume>? Resumes { get; set; } // Add navigation property

    }
}
