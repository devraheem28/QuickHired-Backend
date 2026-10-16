namespace QuickHired.API.DTOs
{
    public class SaveReportDTO
    {
        public string Email { get; set; }
        public int TotalQuestions { get; set; }
        public List<QuestionAnswerDTO> Questions { get; set; }
        public string DurationInSeconds { get; set; }
        public string InterviewType { get; set; }
        public string? TranscriptFileName { get; set; }
        public string? TranscriptFileBase64 { get; set; }
    }

    public class QuestionAnswerDTO
    {
        public string Question { get; set; }
        public string Answer { get; set; }
    }

}
