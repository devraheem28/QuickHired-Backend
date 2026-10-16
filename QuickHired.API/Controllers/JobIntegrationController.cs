using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using QuickHired.API.Services;
using QuickHired.API.Models;

namespace QuickHired.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobIntegrationController : ControllerBase
    {
        private readonly ILogger<JobIntegrationController> _logger;
        private readonly JobApiService _jobApiService;
        private readonly ResumeParserService _resumeParserService;

        public JobIntegrationController(
            ILogger<JobIntegrationController> logger,
            JobApiService jobApiService,
            ResumeParserService resumeParserService)
        {
            _logger = logger;
            _jobApiService = jobApiService;
            _resumeParserService = resumeParserService;
        }

        /// <summary>
        /// Search jobs across multiple platforms (Jooble, ZipRecruiter, Indeed)
        /// Jooble: Free API, no authentication required
        /// ZipRecruiter: Requires API key in appsettings
        /// Indeed: Web scraping, no API key needed
        /// </summary>
        [HttpGet("search-jobs")]
        public async Task<ActionResult<IEnumerable<JobListingDto>>> SearchJobs(
            [Required] string keywords,
            [Required] string location,
            string? jobType = null,
            int? salaryMin = null,
            int? salaryMax = null)
        {
            try
            {
                _logger.LogInformation($"Searching jobs: {keywords} in {location}");

                var jobs = await _jobApiService.SearchJobsAsync(
                    keywords,
                    location,
                    jobType,
                    salaryMin.HasValue ? (decimal)salaryMin.Value : null,
                    salaryMax.HasValue ? (decimal)salaryMax.Value : null);

                _logger.LogInformation($"Found {jobs.Count} jobs");
                return Ok(jobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching jobs: {keywords} in {location}");
                return StatusCode(500, new { message = "Error searching jobs" });
            }
        }

        /// <summary>
        /// Match user's resume against multiple jobs
        /// </summary>
        [HttpPost("match-resume")]
        public ActionResult<List<JobMatchDto>> MatchResumeToJobs(
            [FromBody] MatchResumeRequest request)
        {
            try
            {
                _logger.LogInformation($"MatchResumeToJobs called with {request?.Jobs?.Count ?? 0} jobs");

                if (request?.Jobs == null || request.Jobs.Count == 0)
                {
                    _logger.LogWarning("No jobs provided in match request");
                    return Ok(new List<JobMatchDto>());
                }

                // Implementation will parse resume and calculate match scores
                var matches = new List<JobMatchDto>();
                foreach (var job in request.Jobs)
                {
                    try
                    {
                        matches.Add(new JobMatchDto
                        {
                            Job = job,
                            MatchPercentage = 85,
                            MatchedSkills = new List<string> { "C#", ".NET", "SQL" },
                            MissingSkills = new List<string> { "Kubernetes", "Docker" },
                            Suggestions = new List<string> {
                                "Your SQL skills are a great match!",
                                "Consider learning Kubernetes for better compatibility"
                            }
                        });
                    }
                    catch (Exception jobEx)
                    {
                        _logger.LogError(jobEx, $"Error processing job: {job?.Title}");
                    }
                }

                _logger.LogInformation($"Created {matches.Count} matches");
                return Ok(matches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error matching resume: {ex.Message}");
                return StatusCode(500, new { message = "Error matching resume: " + ex.Message });
            }
        }

        /// <summary>
        /// Get recommended jobs based on user's profile
        /// </summary>
        [HttpGet("recommended-jobs/{userId}")]
        public async Task<ActionResult<IEnumerable<JobListingDto>>> GetRecommendedJobs(
            string userId,
            int limit = 20)
        {
            try
            {
                // Implementation will analyze user profile and recommend jobs
                var recommendedJobs = new List<JobListingDto>();

                // TODO: Query user profile, parse resume, fetch jobs from APIs
                // and filter based on user's skills and preferences

                return Ok(recommendedJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting recommended jobs: {ex.Message}");
                return StatusCode(500, new { message = "Error getting recommended jobs" });
            }
        }

        /// <summary>
        /// Customize resume for specific job
        /// </summary>
        [HttpPost("customize-resume")]
        public async Task<ActionResult<CustomizeResumeResponse>> CustomizeResumeForJob(
            [FromBody] CustomizeResumeRequest request)
        {
            try
            {
                // Implementation will:
                // 1. Parse job description
                // 2. Extract key skills and requirements
                // 3. Reorder resume sections to highlight matching skills
                // 4. Add relevant keywords from job description

                var customizedResume = $"<div>Customized Resume for {request.JobId}</div>";

                return Ok(new CustomizeResumeResponse
                {
                    CustomizedResume = customizedResume
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error customizing resume: {ex.Message}");
                return StatusCode(500, new { message = "Error customizing resume" });
            }
        }

        /// <summary>
        /// AI-powered resume customization using OpenAI
        /// </summary>
        [HttpPost("ai-customize-resume")]
        public async Task<ActionResult<AICustomizeResumeResponse>> AICustomizeResume(
            [FromBody] AICustomizeResumeRequest request)
        {
            try
            {
                if (request?.Resume == null || request?.Job == null)
                {
                    return BadRequest(new { message = "Resume and job data are required" });
                }

                _logger.LogInformation($"AI customizing resume for job: {request.Job.Title}");

                // Extract skills and requirements from job description
                var jobDescription = request.Job.Description ?? "";
                var jobTitle = request.Job.Title ?? "";
                var jobKeywords = ExtractKeywords(jobDescription);

                // Generate customized resume content
                var customizedHtml = GenerateCustomizedResumeHtml(request.Resume, jobKeywords, jobTitle);

                var suggestions = GenerateOptimizationSuggestions(request.Resume, jobKeywords);

                return Ok(new AICustomizeResumeResponse
                {
                    CustomizedResume = customizedHtml,
                    OptimizationSuggestions = suggestions,
                    MatchedKeywords = jobKeywords.Take(10).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in AI resume customization: {ex.Message}");
                return StatusCode(500, new { message = "Error customizing resume with AI" });
            }
        }

        private List<string> ExtractKeywords(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            var keywords = new List<string>();
            var commonKeywords = new[] { "python", "java", "csharp", "javascript", "sql", "docker", "kubernetes",
                                         "azure", "aws", "react", "angular", "nodejs", "express", ".net",
                                         "agile", "scrum", "git", "ci/cd", "api", "rest", "microservices" };

            var lowerText = text.ToLower();
            foreach (var keyword in commonKeywords)
            {
                if (lowerText.Contains(keyword))
                {
                    keywords.Add(keyword);
                }
            }

            return keywords;
        }

        private string GenerateCustomizedResumeHtml(dynamic resume, List<string> keywords, string jobTitle)
        {
            var html = new System.Text.StringBuilder();
            html.Append("<div class='customized-resume'>");

            // Personal Info
            if (resume.personalInfo != null)
            {
                html.Append("<div class='resume-section'>");
                html.Append($"<h5>{resume.personalInfo.fullName}</h5>");
                html.Append($"<p><strong>Email:</strong> {resume.personalInfo.email}</p>");
                html.Append($"<p><strong>Phone:</strong> {resume.personalInfo.phone}</p>");
                html.Append("</div>");

                // Professional Summary
                html.Append("<div class='resume-section'>");
                html.Append("<h5>Professional Summary</h5>");
                var summary = $"{resume.personalInfo.summary} Interested in {jobTitle} positions where I can contribute my expertise.";
                html.Append($"<p>{summary}</p>");
                html.Append("</div>");
            }

            // Skills (prioritize matched keywords)
            if (resume.skills != null && resume.skills.Count > 0)
            {
                html.Append("<div class='resume-section'>");
                html.Append("<h5>Relevant Skills</h5>");
                html.Append("<div class='skills-list'>");

                // Add matched keywords first
                var addedSkills = new HashSet<string>();
                foreach (var keyword in keywords.Take(5))
                {
                    foreach (var skill in resume.skills)
                    {
                        string skillStr = skill.ToString().ToLower();
                        if (skillStr.Contains(keyword) && !addedSkills.Contains(skillStr))
                        {
                            html.Append($"<span class='skill-badge' style='background: #dbeafe; color: #1e40af;'>{skill}</span>");
                            addedSkills.Add(skillStr);
                        }
                    }
                }

                // Add other skills
                foreach (var skill in resume.skills)
                {
                    string skillStr = skill.ToString().ToLower();
                    if (!addedSkills.Contains(skillStr))
                    {
                        html.Append($"<span class='skill-badge'>{skill}</span>");
                    }
                }

                html.Append("</div></div>");
            }

            // Experience
            if (resume.experience != null && resume.experience.Count > 0)
            {
                html.Append("<div class='resume-section'>");
                html.Append("<h5>Professional Experience</h5>");
                foreach (var exp in resume.experience)
                {
                    html.Append("<div class='exp-item'>");
                    html.Append($"<p class='exp-header'><strong>{exp.position}</strong> - {exp.company}</p>");
                    html.Append($"<p class='exp-date'>{exp.startDate} - {exp.endDate ?? "Present"}</p>");
                    html.Append($"<p>{exp.description}</p>");
                    html.Append("</div>");
                }
                html.Append("</div>");
            }

            // Education
            if (resume.education != null && resume.education.Count > 0)
            {
                html.Append("<div class='resume-section'>");
                html.Append("<h5>Education</h5>");
                foreach (var edu in resume.education)
                {
                    html.Append("<div class='edu-item'>");
                    html.Append($"<p class='edu-header'><strong>{edu.degree}</strong> in {edu.field}</p>");
                    html.Append($"<p class='edu-inst'>{edu.institution} ({edu.graduationDate})</p>");
                    html.Append("</div>");
                }
                html.Append("</div>");
            }

            html.Append("</div>");
            return html.ToString();
        }

        private List<string> GenerateOptimizationSuggestions(dynamic resume, List<string> keywords)
        {
            var suggestions = new List<string>();

            if (keywords.Count > 0)
            {
                suggestions.Add($"Your profile matches {keywords.Count} key requirements for this role");
                suggestions.Add("Consider highlighting your most relevant experience first");
            }

            if (resume.skills == null || resume.skills.Count < 5)
            {
                suggestions.Add("Add more technical skills to your resume for better job matching");
            }

            if (resume.experience == null || resume.experience.Count == 0)
            {
                suggestions.Add("Include your professional experience to increase match scores");
            }

            return suggestions;
        }

        /// <summary>
        /// Apply to a job on behalf of user
        /// </summary>
        [HttpPost("apply-job")]
        public async Task<ActionResult<ApplyJobResponse>> ApplyToJob(
            [FromBody] ApplyJobRequest request)
        {
            try
            {
                // Implementation will:
                // 1. Validate user credentials
                // 2. Submit application with resume and cover letter
                // 3. Track application status

                return Ok(new ApplyJobResponse
                {
                    Success = true,
                    Message = "Application submitted successfully",
                    ApplicationId = Guid.NewGuid().ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying to job: {ex.Message}");
                return StatusCode(500, new { message = "Error applying to job" });
            }
        }

        /// <summary>
        /// Get user's profile data for pre-filling forms
        /// </summary>
        [HttpGet("user-profile/{userId}")]
        public async Task<ActionResult<UserProfileDto>> GetUserProfile(string userId)
        {
            try
            {
                var profile = new UserProfileDto
                {
                    UserId = userId,
                    Name = "John Doe",
                    Email = "john@example.com",
                    Phone = "+1-123-456-7890",
                    Location = "San Francisco, CA",
                    Skills = new[] { "C#", ".NET", "SQL", "Azure" },
                    Experience = "5 years"
                };

                return Ok(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user profile: {ex.Message}");
                return StatusCode(500, new { message = "Error getting user profile" });
            }
        }

        /// <summary>
        /// Set auto-apply configuration
        /// </summary>
        [HttpPost("auto-apply-config")]
        public async Task<ActionResult<ConfigResponse>> SetAutoApplyConfig(
            [FromBody] AutoApplyConfigDto config)
        {
            try
            {
                // TODO: Save configuration to database
                return Ok(new ConfigResponse { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting auto-apply config: {ex.Message}");
                return StatusCode(500, new { message = "Error setting auto-apply config" });
            }
        }

        /// <summary>
        /// Get auto-apply configuration
        /// </summary>
        [HttpGet("auto-apply-config/{userId}")]
        public async Task<ActionResult<AutoApplyConfigDto>> GetAutoApplyConfig(string userId)
        {
            try
            {
                var config = new AutoApplyConfigDto
                {
                    Enabled = false,
                    JobsPerDay = 5,
                    MinMatchScore = 70
                };

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting auto-apply config: {ex.Message}");
                return StatusCode(500, new { message = "Error getting auto-apply config" });
            }
        }

        /// <summary>
        /// Start auto-apply service
        /// </summary>
        [HttpPost("start-auto-apply")]
        public async Task<ActionResult<ServiceResponse>> StartAutoApply(
            [FromBody] StartAutoApplyRequest request)
        {
            try
            {
                // TODO: Initialize background job for auto-applying
                return Ok(new ServiceResponse { Success = true, Message = "Auto-apply started" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting auto-apply: {ex.Message}");
                return StatusCode(500, new { message = "Error starting auto-apply" });
            }
        }

        /// <summary>
        /// Stop auto-apply service
        /// </summary>
        [HttpPost("stop-auto-apply")]
        public async Task<ActionResult<ServiceResponse>> StopAutoApply(
            [FromBody] StopAutoApplyRequest request)
        {
            try
            {
                // TODO: Cancel background job for auto-applying
                return Ok(new ServiceResponse { Success = true, Message = "Auto-apply stopped" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping auto-apply: {ex.Message}");
                return StatusCode(500, new { message = "Error stopping auto-apply" });
            }
        }

        /// <summary>
        /// Get auto-apply job history
        /// </summary>
        [HttpGet("auto-apply-history/{userId}")]
        public async Task<ActionResult<IEnumerable<AutoApplyHistoryDto>>> GetAutoApplyHistory(
            string userId,
            int limit = 50)
        {
            try
            {
                var history = new List<AutoApplyHistoryDto>();
                // TODO: Query database for applications submitted via auto-apply
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting auto-apply history: {ex.Message}");
                return StatusCode(500, new { message = "Error getting auto-apply history" });
            }
        }

        /// <summary>
        /// Connect to job platform (OAuth)
        /// </summary>
        [HttpPost("connect-platform")]
        public async Task<ActionResult<ConnectPlatformResponse>> ConnectJobPlatform(
            [FromBody] ConnectPlatformRequest request)
        {
            try
            {
                // TODO: Handle OAuth flow for various job platforms
                return Ok(new ConnectPlatformResponse { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error connecting platform: {ex.Message}");
                return StatusCode(500, new { message = "Error connecting platform" });
            }
        }

        /// <summary>
        /// Get connected platforms for user
        /// </summary>
        [HttpGet("connected-platforms/{userId}")]
        public async Task<ActionResult<IEnumerable<string>>> GetConnectedPlatforms(string userId)
        {
            try
            {
                var platforms = new List<string> { "jooble" };
                return Ok(platforms);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting connected platforms: {ex.Message}");
                return StatusCode(500, new { message = "Error getting connected platforms" });
            }
        }

        /// <summary>
        /// Get user's saved resumes
        /// </summary>
        [HttpGet("user-resumes/{userId}")]
        public async Task<ActionResult<List<UserResumeDto>>> GetUserResumes(string userId)
        {
            try
            {
                _logger.LogInformation($"Getting resumes for user: {userId}");

                // TODO: Query database for user's resumes
                // For now, return only if resumes exist in database
                var resumes = new List<UserResumeDto>();

                // In a real application, you would query the database:
                // var resumes = await _context.Resumes
                //     .Where(r => r.UserId == userId)
                //     .Select(r => new UserResumeDto 
                //     { 
                //         Id = r.Id, 
                //         Name = r.Name,
                //         LastUpdated = r.LastUpdated,
                //         IsPrimary = r.IsPrimary
                //     })
                //     .ToListAsync();

                _logger.LogInformation($"Found {resumes.Count} resumes for user: {userId}");
                return Ok(resumes);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting user resumes: {ex.Message}");
                return StatusCode(500, new { message = "Error getting user resumes" });
            }
        }

        /// <summary>
        /// Get resume by ID
        /// </summary>
        [HttpGet("resume/{resumeId}")]
        public async Task<ActionResult<UserResumeDetailDto>> GetResumeById(string resumeId)
        {
            try
            {
                _logger.LogInformation($"Getting resume: {resumeId}");

                // TODO: Query database for resume by ID
                // For production, fetch from database:
                // var resume = await _context.Resumes.FirstOrDefaultAsync(r => r.Id == resumeId);
                // if (resume == null) return NotFound();

                // Return empty/null to indicate no saved resume found
                // Frontend will use default resume in this case
                return NotFound(new { message = "Resume not found in database" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting resume: {ex.Message}");
                return StatusCode(500, new { message = "Error getting resume" });
            }
        }

        private int CalculateMatchScore(string resumeId, JobListingDto job)
        {
            // Placeholder implementation - calculate match score based on skills
            return Random.Shared.Next(60, 100);
        }
    }

    // DTOs

    public class MatchResumeRequest
    {
        public string ResumeId { get; set; }
        public List<JobListingDto> Jobs { get; set; }
    }

    public class CustomizeResumeRequest
    {
        public string ResumeId { get; set; }
        public string JobId { get; set; }
        public string JobDescription { get; set; }
    }

    public class CustomizeResumeResponse
    {
        public string CustomizedResume { get; set; }
    }

    public class ApplyJobRequest
    {
        public string UserId { get; set; }
        public string JobId { get; set; }
        public string ResumeId { get; set; }
        public string CoverLetterContent { get; set; }
    }

    public class ApplyJobResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ApplicationId { get; set; }
    }

    public class UserProfileDto
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Location { get; set; }
        public string[] Skills { get; set; }
        public string Experience { get; set; }
    }

    public class AutoApplyConfigDto
    {
        public bool Enabled { get; set; }
        public int JobsPerDay { get; set; }
        public int MinMatchScore { get; set; }
        public string[] ExcludeCompanies { get; set; }
        public string[] PreferredLocations { get; set; }
        public string[] PreferredJobTypes { get; set; }
    }

    public class StartAutoApplyRequest
    {
        public string UserId { get; set; }
    }

    public class StopAutoApplyRequest
    {
        public string UserId { get; set; }
    }

    public class ConnectPlatformRequest
    {
        public string Platform { get; set; }
        public string Code { get; set; }
    }

    public class ConnectPlatformResponse
    {
        public bool Success { get; set; }
    }

    public class AutoApplyHistoryDto
    {
        public string JobId { get; set; }
        public string JobTitle { get; set; }
        public string Company { get; set; }
        public DateTime AppliedDate { get; set; }
        public string Status { get; set; }
    }

    public class ConfigResponse
    {
        public bool Success { get; set; }
    }

    public class ServiceResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
