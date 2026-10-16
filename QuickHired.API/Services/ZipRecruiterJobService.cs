using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;
using QuickHired.API.Models;

namespace QuickHired.API.Services
{
    /// <summary>
    /// ZipRecruiter Job Search API Service
    /// Requires API key from https://www.ziprecruiter.com/api
    /// </summary>
    public class ZipRecruiterJobService
    {
        private readonly ILogger<ZipRecruiterJobService> _logger;
        private readonly IConfiguration _configuration;
        private const string ZipRecruiterApiUrl = "https://api.ziprecruiter.com/jobs/v1/search";

        public ZipRecruiterJobService(ILogger<ZipRecruiterJobService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<JobListingDto>> SearchJobsAsync(string keywords, string location, string? jobType = null)
        {
            try
            {
                _logger.LogInformation($"[ZipRecruiter] Request: keywords={keywords}, location={location}, jobType={jobType}");

                var apiKey = _configuration["ZipRecruiter:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("ZipRecruiter API key is missing in appsettings.json (ZipRecruiter:ApiKey)");
                    // Return empty list if API key not configured
                    return new List<JobListingDto>();
                }

                var client = new RestClient(ZipRecruiterApiUrl);
                var request = new RestRequest(string.Empty, Method.Get);

                // Add query parameters
                request.AddParameter("api_key", apiKey);
                request.AddParameter("search", keywords);
                request.AddParameter("location", location);
                request.AddParameter("days_ago", 30); // Last 30 days
                request.AddParameter("jobs_per_page", 20);

                // Add job type filter if specified
                if (!string.IsNullOrWhiteSpace(jobType))
                {
                    request.AddParameter("employment_type", MapJobTypeToZipRecruiter(jobType));
                }

                _logger.LogInformation($"[ZipRecruiter] Calling API with keywords={keywords}, location={location}");

                var response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    _logger.LogError($"[ZipRecruiter] API error: {response.StatusCode} - {response.Content}");
                    return new List<JobListingDto>();
                }

                var jobs = ParseZipRecruiterResponse(response.Content ?? "");
                _logger.LogInformation($"[ZipRecruiter] Found {jobs.Count} jobs");
                return jobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZipRecruiter] Error searching jobs");
                return new List<JobListingDto>();
            }
        }

        /// <summary>
        /// Parse ZipRecruiter API response
        /// </summary>
        private List<JobListingDto> ParseZipRecruiterResponse(string content)
        {
            var jobs = new List<JobListingDto>();

            try
            {
                var jsonData = JObject.Parse(content);
                var jobsList = jsonData["jobs"] as JArray;

                if (jobsList == null || jobsList.Count == 0)
                {
                    _logger.LogInformation("[ZipRecruiter] No jobs found in response");
                    return jobs;
                }

                foreach (var jobToken in jobsList)
                {
                    try
                    {
                        var job = jobToken as JObject;
                        if (job == null) continue;

                        var jobDto = new JobListingDto
                        {
                            Id = job["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                            Title = job["name"]?.ToString() ?? "Unknown Position",
                            Company = job["hiring_company"]?["name"]?.ToString() ?? "Unknown Company",
                            Location = job["location"]?["name"]?.ToString() ?? "Remote",
                            Description = job["snippet"]?.ToString() ?? job["job_description"]?.ToString() ?? "",
                            Salary = ExtractSalary(job),
                            JobType = ExtractJobType(job),
                            PostedDate = TryParsePostedDate(job["posted_time"]?.ToString()),
                            Source = "ziprecruiter",
                            Url = job["url"]?.ToString() ?? "",
                            Requirements = ExtractRequirements(job),
                            Skills = ExtractSkills(job)
                        };

                        jobs.Add(jobDto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ZipRecruiter] Error parsing individual job");
                        continue;
                    }
                }

                _logger.LogInformation($"[ZipRecruiter] Successfully parsed {jobs.Count} jobs");
                return jobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZipRecruiter] Error parsing response");
                return jobs;
            }
        }

        /// <summary>
        /// Extract salary from job listing
        /// </summary>
        private string ExtractSalary(JObject job)
        {
            try
            {
                var salaryMin = job["salary_min"]?.Value<int>();
                var salaryMax = job["salary_max"]?.Value<int>();
                var salaryPeriod = job["salary_period"]?.ToString() ?? "year";

                if (salaryMin.HasValue && salaryMax.HasValue)
                {
                    return $"${salaryMin:N0} - ${salaryMax:N0} per {salaryPeriod}";
                }
                else if (salaryMin.HasValue)
                {
                    return $"${salaryMin:N0}+ per {salaryPeriod}";
                }
                else if (salaryMax.HasValue)
                {
                    return $"Up to ${salaryMax:N0} per {salaryPeriod}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZipRecruiter] Error extracting salary");
            }

            return null;
        }

        /// <summary>
        /// Extract job type (full-time, part-time, etc.)
        /// </summary>
        private string ExtractJobType(JObject job)
        {
            try
            {
                var employmentType = job["employment_type"]?.ToString();
                return !string.IsNullOrWhiteSpace(employmentType) ? employmentType.ToLower() : "full-time";
            }
            catch
            {
                return "full-time";
            }
        }

        /// <summary>
        /// Extract required skills from job description
        /// </summary>
        private string[] ExtractSkills(JObject job)
        {
            var skills = new List<string>();

            try
            {
                var description = (job["job_description"]?.ToString() ?? job["snippet"]?.ToString() ?? "").ToLower();

                // Common skill keywords to look for
                var skillKeywords = new[]
                {
                    "c#", ".net", "java", "python", "javascript", "typescript", "react", "angular", "vue",
                    "sql", "mysql", "mongodb", "postgresql", "redis", "docker", "kubernetes", "aws", "azure",
                    "git", "agile", "scrum", "rest api", "graphql", "linux", "windows", "html", "css",
                    "nodejs", "express", "asp.net", "spring", "django", "flask", "golang", "rust",
                    "ci/cd", "jenkins", "gitlab", "github", "jira", "confluence", "communication", "leadership"
                };

                foreach (var skill in skillKeywords)
                {
                    if (description.Contains(skill))
                    {
                        skills.Add(skill);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZipRecruiter] Error extracting skills");
            }

            return skills.Distinct().ToArray();
        }

        /// <summary>
        /// Extract requirements from job listing
        /// </summary>
        private string[] ExtractRequirements(JObject job)
        {
            var requirements = new List<string>();

            try
            {
                var description = job["job_description"]?.ToString() ?? job["snippet"]?.ToString() ?? "";

                // Split by common requirement indicators
                var lines = description.Split(new[] { "\n", "\r\n", "•", "-" }, StringSplitOptions.RemoveEmptyEntries);

                requirements = lines
                    .Where(l => l.Length > 10 && l.Length < 200)
                    .Select(l => l.Trim())
                    .Take(5)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZipRecruiter] Error extracting requirements");
            }

            return requirements.ToArray();
        }

        /// <summary>
        /// Map our job type to ZipRecruiter's format
        /// </summary>
        private string MapJobTypeToZipRecruiter(string jobType)
        {
            return jobType?.ToLower() switch
            {
                "full-time" => "full_time",
                "part-time" => "part_time",
                "contract" => "contract",
                "temporary" => "temporary",
                "remote" => "full_time", // ZipRecruiter doesn't have "remote" as type
                _ => ""
            };
        }

        /// <summary>
        /// Try to parse posted date from string
        /// </summary>
        private DateTime TryParsePostedDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return DateTime.UtcNow;

            // Try parsing ISO format
            if (DateTime.TryParse(dateString, out var result))
                return result;

            return DateTime.UtcNow;
        }
    }
}

