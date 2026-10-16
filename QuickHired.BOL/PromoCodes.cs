using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("PromoCodes")]
    public class PromoCodes : BaseEntity
    {
        public string Code { get; set; }
        public decimal Percentage { get; set; } // Discount percentage
        public DateTime ExpirationDate { get; set; }
        public string SubscriptionName { get; set; }
        public string PlanId { get; set; }
        public string StripeButtonId { get; set; }
        public string PublishableKey { get; set; }


    }

}
