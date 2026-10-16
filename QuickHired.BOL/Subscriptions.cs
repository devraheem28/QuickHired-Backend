using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("Subscriptions")]
    public class Subscriptions : BaseEntity
    {
        public string SubscriptionName { get; set; }
        public string AIInterviewCopilot { get; set; }
        public string SessionLength { get; set; }

        public string AIMockInterview { get; set; }

        public string AIInterviewReports { get; set; }

        public int MeetingRecording { get; set; }
        public decimal Price { get; set; }
    }

}
