namespace QuickHired.API.DTOs
{
    public class InterviewQuestionsDTO
    {
        /// <summary>
        /// Gets or sets the ID of the interview question.
        /// </summary>
        public int Id { get; set; }

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
    }

}
