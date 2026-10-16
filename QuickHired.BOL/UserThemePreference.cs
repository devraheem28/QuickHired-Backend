using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("UserThemePreference")]
    public class UserThemePreference
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Theme { get; set; } = "system";
        public DateTime CreatedAt { get; set; }
    }

}
