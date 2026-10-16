namespace QuickHired.API.DTOs
{
    public class MeetingRequestDTO
    {
        public string Topic { get; set; }
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
    }
}
