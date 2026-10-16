namespace QuickHired.API.DTOs
{
    public class GoogleMeetRequestDTO
    {
        public string Topic { get; set; }
        public DateTime StartTime { get; set; }
        public int Duration { get; set; } // Duration in minutes
        public string ParticipantEmail { get; set; }
    }
}
