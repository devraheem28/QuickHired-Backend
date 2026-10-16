using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuickHired.BOL
{
    /// <summary>
    /// Represents an interview question asked during a meeting, along with its AI-generated answer.
    /// </summary>
    [Table("InterviewQuestions")]
    public class InterviewQuestions : BaseEntity
    {
        /// <summary>
        /// Gets or sets the ID of the meeting where the question was asked.
        /// </summary>
        public int MeetingId { get; set; }

        /// <summary>
        /// Gets or sets the question asked during the interview.
        /// </summary>
        public string Question { get; set; }

        /// <summary>
        /// Gets or sets the AI-generated answer for the question.
        /// </summary>
        public string AIAnswer { get; set; }

        /// <summary>
        /// Navigation property for the related meeting.
        /// </summary>
        [ForeignKey("MeetingId")]
        public virtual Meetings Meetings { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InterviewQuestion"/> class.
        /// </summary>
        public InterviewQuestions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InterviewQuestion"/> class with specific values.
        /// </summary>
        /// <param name="meetingId">The ID of the meeting where the question was asked.</param>
        /// <param name="question">The question asked during the interview.</param>
        /// <param name="aiAnswer">The AI-generated answer for the question.</param>
        public InterviewQuestions(int meetingId, string question, string aiAnswer)
        {
            MeetingId = meetingId;
            Question = question;
            AIAnswer = aiAnswer;
        }
    }
}
