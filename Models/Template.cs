using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeBuilderBackend.Models
{
    public class Template
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        
        public string Content { get; set; }
        // Optional: Add a content type field
        public string ContentType { get; set; } = "html";
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; } = DateTime.UtcNow;
       
    }
}
