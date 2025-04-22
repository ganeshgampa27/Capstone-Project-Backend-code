using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeBuilderBackend.Models
{
    public class Resume
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResumeId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int TemplateId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Resume name must be between 3 and 100 characters")]
        public string Name { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public UserRegistration? User { get; set; }

        [ForeignKey("TemplateId")]
        public Template? Template { get; set; }
    }
}