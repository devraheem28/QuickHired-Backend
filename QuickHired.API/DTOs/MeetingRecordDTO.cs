namespace QuickHired.API.DTOs
{
    public class MeetingRecordDTO
    {
        public string UserId { get; set; }
        public string CompanyName { get; set; }
        public string JobTitle { get; set; }
        public string Summary { get; set; }
        public string Duration { get; set; }
        public string SummaryFileName { get; set; }
        public string SummaryFileBase64 { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TranscriptFileName { get; set; }
        public string TranscriptFileBase64 { get; set; }
    }
}
