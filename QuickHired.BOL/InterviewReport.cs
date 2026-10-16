using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("InterviewReport")]
    public class InterviewReport : BaseEntity
    {
        public string UserId { get; set; }
        public int PersonaId { get; set; }
        public string Duration { get; set; }
        public double ScorePercentage { get; set; }
        public int TotalQuestions { get; set; }
        public string? InterviewType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? TranscriptFileName { get; set; }
        public byte[]? TranscriptFileContent { get; set; }
        public string? Improvements { get; set; }
        public string? Weaknesses { get; set; }
    }

}
