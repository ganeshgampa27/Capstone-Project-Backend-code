namespace ResumeBuilderBackend.DTOs
{
    public class ResumeDTO
    {
        public int ResumeId { get; set; }
        public int UserId { get; set; }
        public int TemplateId { get; set; }
        public string Name { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
