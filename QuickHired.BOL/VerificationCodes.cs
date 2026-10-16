using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("VerificationCodes")]
    public class VerificationCodes : BaseEntity
    {
        public string UserId { get; set; }
        public string Code { get; set; }
        public DateTime ExpiryTime { get; set; }
        public bool IsVerified { get; set; }
    }

}
