namespace QuickHired.API.DTOs
{
    public class TeamsMeetingDTO
    {
        public string Topic { get; set; }
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
        public string AttendeeEmail { get; set; }
    }
}
