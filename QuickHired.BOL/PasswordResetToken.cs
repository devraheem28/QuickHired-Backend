using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    public class PasswordResetToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public AppUsers User { get; set; }
        public string Token { get; set; }
        public DateTime ExpirationDate { get; set; }
    }
}
