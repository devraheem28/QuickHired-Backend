using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("UserSubscriptions")]
    public class UserSubscriptions : BaseEntity
    {
        public string UserId { get; set; }
        public string PlanName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? SubscriptionKey { get; set; }
        public string? CustomerId { get; set; }
        public bool Status { get; set; } = true; // true = active, false = cancelled
        public string? CancellationReason { get; set; }
        public DateTime? CancelledDate { get; set; }
    }
}
