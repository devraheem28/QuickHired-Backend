using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuickHired.BOL
{
    /// <summary>
    /// Represents a meeting session with a user on a specific platform like Zoom or Teams.
    /// </summary>
    [Table("Meetings")]
    public class Meetings : BaseEntity
    {
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

        /// <summary>
        /// Navigation property for the user who created the meeting.
        /// </summary>
        [ForeignKey("UserId")]
        public virtual AppUsers User { get; set; }

        /// <summary>
        /// Navigation property for the list of interview questions related to the meeting.
        /// </summary>
        public virtual ICollection<InterviewQuestions> InterviewQuestions { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Meeting"/> class.
        /// </summary>
        public Meetings()
        {
            InterviewQuestions = new List<InterviewQuestions>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Meeting"/> class with specific values.
        /// </summary>
        /// <param name="userId">The ID of the user who scheduled the meeting.</param>
        /// <param name="meetingPlatform">The platform used for the meeting.</param>
        /// <param name="meetingLink">The link to join the meeting.</param>
        /// <param name="startTime">The scheduled start time of the meeting.</param>
        public Meetings(string userId, string meetingPlatform, string meetingLink, DateTime startTime)
        {
            UserId = userId;
            MeetingPlatform = meetingPlatform;
            MeetingLink = meetingLink;
            StartTime = startTime;
            InterviewQuestions = new List<InterviewQuestions>();
        }
    }
}
