namespace ResumeBuilderBackend.DTOs
{
    public class UpdateTemplateDTO
    {
        public string Name { get; set; }
        public string Content { get; set; }
        public string ContentType { get; set; } = "html";
    }
}