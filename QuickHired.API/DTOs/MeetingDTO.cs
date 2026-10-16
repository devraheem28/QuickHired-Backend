namespace QuickHired.API.DTOs
{
    public class MeetingDTO
    {
        /// <summary>
        /// Gets or sets the meeting ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID associated with the meeting.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the platform where the meeting is hosted (e.g., Zoom, Teams).
        /// </summary>
        public string MeetingPlatform { get; set; }

        /// <summary>
        /// Gets or sets the meeting link.
        /// </summary>
        public string MeetingLink { get; set; }

        /// <summary>
        /// Gets or sets the start time of the meeting.
        /// </summary>
        public DateTime StartTime { get; set; }
    }

}
