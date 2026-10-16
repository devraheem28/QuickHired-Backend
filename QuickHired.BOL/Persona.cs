using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    [Table("Persona")]
    public class Persona : BaseEntity
    {
        public string? UserId { get; set; }  // Link to current user
        public string? CompanyName { get; set; }
        public string? CompanyDescription { get; set; }
        public string? JobTitle { get; set; }
        public string? JobDescription { get; set; }
        public string? ResumePath { get; set; }
        public string? InterviewDomain { get; set; }
        public string? InterviewType { get; set; }
        public DateTime CreatedAt { get; set; }

    }

}
