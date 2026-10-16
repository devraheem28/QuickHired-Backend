using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("MeetingRecord")]
    public class MeetingRecord : BaseEntity
    {
        public string UserId { get; set; }
        public string CompanyName { get; set; }
        public string JobTitle { get; set; }
        public string Summary { get; set; }
        public string Duration { get; set; }
        public string SummaryFileName { get; set; }
        public byte[] SummaryFileContent { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TranscriptFileName { get; set; }
        public byte[] TranscriptFileContent { get; set; }
    }

}
