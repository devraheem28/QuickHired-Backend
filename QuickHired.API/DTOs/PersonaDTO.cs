namespace QuickHired.API.DTOs
{
    public class PersonaDTO
    {
        public string? UserId { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyDescription { get; set; }
        public string? JobTitle { get; set; }
        public string? JobDescription { get; set; }
        public IFormFile? ResumeFile { get; set; }
        public string? InterviewDomain { get; set; }
        public string? InterviewType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
