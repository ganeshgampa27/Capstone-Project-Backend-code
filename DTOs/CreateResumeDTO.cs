namespace ResumeBuilderBackend.DTOs
{
    public class CreateResumeDTO
    {
        public int UserId { get; set; }
        public int TemplateId { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
    }
}