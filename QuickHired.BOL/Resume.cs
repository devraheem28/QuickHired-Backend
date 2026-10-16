using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.BOL
{
    public class Resume
    {
        public Guid Id { get; set; }
        public string ResumeName { get; set; } = default!;
        public string UserId { get; set; } = default!;

        // Personal
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Location { get; set; } = "";
        public string? LinkedIn { get; set; }
        public string? Portfolio { get; set; }
        public string Summary { get; set; } = "";

        // Collections
        public List<Experience> Experience { get; set; } = new();
        public List<Education> Education { get; set; } = new();
        public List<Project> Projects { get; set; } = new();
        public List<Certification> Certifications { get; set; } = new();
        public List<LanguageItem> Languages { get; set; } = new();
        public List<SkillItem> Skills { get; set; } = new();

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class Experience
    {
        public Guid Id { get; set; }
        public Guid ResumeId { get; set; }
        public Resume Resume { get; set; } = default!;
        public string Company { get; set; } = "";
        public string Position { get; set; } = "";
        public string StartDate { get; set; } = "";  // yyyy-MM
        public string? EndDate { get; set; }         // yyyy-MM
        public bool Current { get; set; }
        public string Description { get; set; } = "";
    }

    public class Education
    {
        public Guid Id { get; set; }
        public Guid ResumeId { get; set; }
        public Resume Resume { get; set; } = default!;
        public string Institution { get; set; } = "";
        public string Degree { get; set; } = "";
        public string Field { get; set; } = "";
        public string GraduationDate { get; set; } = ""; // yyyy-MM or yyyy
        public string? Gpa { get; set; }
    }

    public class Project
    {
        public Guid Id { get; set; }
        public Guid ResumeId { get; set; }
        public Resume Resume { get; set; } = default!;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Technologies { get; set; } = "";
        public string? Link { get; set; }
    }

    public class Certification
    {
        public Guid Id { get; set; }
        public Guid ResumeId { get; set; }
        public Resume Resume { get; set; } = default!;
        public string Name { get; set; } = "";
    }

    public class LanguageItem
    {
        public Guid Id { get; set; }
        public Guid ResumeId { get; set; }
        public Resume Resume { get; set; } = default!;
        public string Name { get; set; } = "";
    }

    public class SkillItem
    {
        public Guid Id { get; set; }
        public Guid ResumeId { get; set; }
        public Resume Resume { get; set; } = default!;
        public string Name { get; set; } = "";
    }

}
