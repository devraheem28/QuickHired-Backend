using System;
using System.Collections.Generic;

namespace QuickHired.API.Models
{
    /// <summary>
    /// Job listing DTO with all job information from job platforms
    /// </summary>
    public class JobListingDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public string Company { get; set; } = "";
        public string Location { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Salary { get; set; }
        public string JobType { get; set; } = "Full-time";
        public DateTime PostedDate { get; set; } = DateTime.UtcNow;
        public int? Applicants { get; set; }
        public decimal? MatchScore { get; set; }
        public string Source { get; set; } = "jooble"; // jooble
        public string Url { get; set; } = "";
        public string[]? Requirements { get; set; }
        public string[]? Skills { get; set; }
    }

    /// <summary>
    /// Job match result showing how well a resume matches a job
    /// </summary>
    public class JobMatchDto
    {
        public JobListingDto Job { get; set; } = new();
        public int MatchPercentage { get; set; }
        public List<string> MatchedSkills { get; set; } = new();
        public List<string> MissingSkills { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// Match resume request body
    /// </summary>
    public class MatchResumeRequest
    {
        public string ResumeId { get; set; } = "";
        public List<JobListingDto> Jobs { get; set; } = new();
    }

    /// <summary>
    /// Customize resume request body
    /// </summary>
    public class CustomizeResumeRequest
    {
        public string ResumeId { get; set; } = "";
        public string JobId { get; set; } = "";
        public string JobDescription { get; set; } = "";
    }

    /// <summary>
    /// Customize resume response
    /// </summary>
    public class CustomizeResumeResponse
    {
        public string CustomizedResume { get; set; } = "";
    }

    /// <summary>
    /// AI-powered resume customization request
    /// </summary>
    public class AICustomizeResumeRequest
    {
        public dynamic Resume { get; set; }
        public JobListingDto Job { get; set; } = new();
    }

    /// <summary>
    /// AI-powered resume customization response
    /// </summary>
    public class AICustomizeResumeResponse
    {
        public string CustomizedResume { get; set; } = "";
        public List<string> OptimizationSuggestions { get; set; } = new();
        public List<string> MatchedKeywords { get; set; } = new();
    }

    /// <summary>
    /// Apply job request
    /// </summary>
    public class ApplyJobRequest
    {
        public string UserId { get; set; } = "";
        public string JobId { get; set; } = "";
        public string ResumeId { get; set; } = "";
        public string? CoverLetterContent { get; set; }
    }

    /// <summary>
    /// Apply job response
    /// </summary>
    public class ApplyJobResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? ApplicationId { get; set; }
    }

    /// <summary>
    /// User profile DTO for pre-filling application forms
    /// </summary>
    public class UserProfileDto
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Location { get; set; } = "";
        public string[] Skills { get; set; } = Array.Empty<string>();
        public string Experience { get; set; } = "";
    }

    /// <summary>
    /// Auto-apply configuration
    /// </summary>
    public class AutoApplyConfigDto
    {
        public bool Enabled { get; set; }
        public int JobsPerDay { get; set; } = 5;
        public int MinMatchScore { get; set; } = 70;
        public string[] ExcludeCompanies { get; set; } = Array.Empty<string>();
        public string[] PreferredLocations { get; set; } = Array.Empty<string>();
        public string[] PreferredJobTypes { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Auto-apply history entry
    /// </summary>
    public class AutoApplyHistoryDto
    {
        public string JobId { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string Company { get; set; } = "";
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; } = "pending"; // pending, applied, rejected, viewed
    }

    /// <summary>
    /// Connect platform request
    /// </summary>
    public class ConnectPlatformRequest
    {
        public string Platform { get; set; } = "";
        public string Code { get; set; } = "";
    }

    /// <summary>
    /// Generic service response
    /// </summary>
    public class ServiceResponse
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Configuration response
    /// </summary>
    public class ConfigResponse
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Start auto-apply request
    /// </summary>
    public class StartAutoApplyRequest
    {
        public string UserId { get; set; } = "";
    }

    /// <summary>
    /// Stop auto-apply request
    /// </summary>
    public class StopAutoApplyRequest
    {
        public string UserId { get; set; } = "";
    }

    /// <summary>
    /// Resume data extracted from uploaded resume files
    /// </summary>
    public class ResumeDataDto
    {
        public string RawContent { get; set; } = "";
        public List<string> Skills { get; set; } = new();
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int ExperienceYears { get; set; }
        public List<string> Education { get; set; } = new();
    }

    /// <summary>
    /// User resume summary for listing
    /// </summary>
    public class UserResumeDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public bool IsPrimary { get; set; }
    }

    /// <summary>
    /// User resume detail with full data
    /// </summary>
    public class UserResumeDetailDto
    {
        public string Id { get; set; } = "";
        public dynamic Data { get; set; }
    }
}
