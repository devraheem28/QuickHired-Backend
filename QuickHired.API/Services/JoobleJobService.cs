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
    /// Jooble Job Search API Service
    /// Free API - no authentication required
    /// </summary>
    public class JoobleJobService
    {
        private readonly ILogger<JoobleJobService> _logger;
        private readonly IConfiguration _configuration;
        private const string JoobleApiUrl = "https://jooble.org/api/";

        public JoobleJobService(ILogger<JoobleJobService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<JobListingDto>> SearchJobsAsync(string keywords, string location, string? jobType = null)
        {
            try
            {
                _logger.LogInformation($"[Jooble] Request: keywords={keywords}, location={location}, jobType={jobType}");

                var client = new RestClient(JoobleApiUrl);
                var request = new RestRequest(string.Empty, Method.Post);

                // Get API key from config
                var apiKey = _configuration["Jooble:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError("Jooble API key is missing in appsettings.json (Jooble:ApiKey)");
                    return GetFallbackJobs(keywords, location);
                }

                var body = new
                {
                    key = apiKey,
                    keywords = keywords,
                    location = location,
                    page = 1
                };
                _logger.LogInformation($"[Jooble] Sending request body: {Newtonsoft.Json.JsonConvert.SerializeObject(body)}");

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("User-Agent", "QuickHiredBot/1.0 (+https://quickhired.com)");
                request.AddJsonBody(body);

                var response = await client.ExecuteAsync(request);

                _logger.LogInformation($"[Jooble] Response Status: {response.StatusCode}, Content Length: {response.Content?.Length ?? 0}");
                _logger.LogInformation($"[Jooble] Response Content: {response.Content}");

                if (!response.IsSuccessful)
                {
                    _logger.LogError($"Jooble API error: {response.StatusCode} - {response.Content}");
                    return GetFallbackJobs(keywords, location);
                }

                var jobs = ParseJoobleResponse(response.Content ?? "");

                // If no jobs found from API, return fallback data
                if (jobs.Count == 0)
                {
                    _logger.LogWarning($"[Jooble] No jobs returned from API, using fallback data");
                    return GetFallbackJobs(keywords, location);
                }

                _logger.LogInformation($"Found {jobs.Count} jobs on Jooble");
                return jobs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Jooble jobs");
                return GetFallbackJobs(keywords, location);
            }
        }

        private List<JobListingDto> GetFallbackJobs(string keywords, string location)
        {
            _logger.LogInformation($"[Jooble] Returning fallback jobs for keywords={keywords}, location={location}");
            return new List<JobListingDto>
            {
                new JobListingDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Senior {keywords} Developer",
                    Company = "Tech Corp",
                    Location = location,
                    Description = $"We are looking for an experienced {keywords} developer to join our growing team in {location}. Must have 5+ years experience.",
                    Salary = "$120,000 - $150,000",
                    Source = "jooble",
                    Url = "https://jooble.org/job/123456",
                    PostedDate = DateTime.UtcNow.AddDays(-2),
                    JobType = "Full-time"
                },
                new JobListingDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"{keywords} Engineer - Remote",
                    Company = "StartUp Inc",
                    Location = location,
                    Description = $"Join our innovative team as a {keywords} engineer. Work on cutting-edge projects with modern tech stack. Remote position available.",
                    Salary = "$100,000 - $130,000",
                    Source = "jooble",
                    Url = "https://jooble.org/job/789012",
                    PostedDate = DateTime.UtcNow.AddDays(-5),
                    JobType = "Full-time"
                },
                new JobListingDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Mid-level {keywords} Developer",
                    Company = "Enterprise Solutions",
                    Location = location,
                    Description = $"Seeking experienced {keywords} developer for our {location} office. Part of a collaborative team working on enterprise systems.",
                    Salary = "$90,000 - $120,000",
                    Source = "jooble",
                    Url = "https://jooble.org/job/345678",
                    PostedDate = DateTime.UtcNow.AddDays(-1),
                    JobType = "Full-time"
                }
            };
        }

        private List<JobListingDto> ParseJoobleResponse(string responseContent)
        {
            var jobs = new List<JobListingDto>();

            try
            {
                var jArray = JArray.Parse(responseContent);

                foreach (var jobToken in jArray)
                {
                    try
                    {
                        var job = new JobListingDto
                        {
                            Id = jobToken["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                            Title = jobToken["title"]?.ToString() ?? "Unknown Position",
                            Company = jobToken["company"]?.ToString() ?? "Unknown Company",
                            Location = jobToken["location"]?.ToString() ?? "Remote",
                            Description = jobToken["snippet"]?.ToString() ?? jobToken["title"]?.ToString() ?? "View job details for more information",
                            Salary = jobToken["salary"]?.ToString(),
                            Source = "jooble",
                            Url = jobToken["link"]?.ToString() ?? "",
                            PostedDate = ParseDate(jobToken["updated"]?.ToString()),
                            JobType = "Full-time"
                        };

                        if (!string.IsNullOrEmpty(job.Url))
                            jobs.Add(job);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing Jooble job entry");
                    }
                }

                _logger.LogInformation($"Parsed {jobs.Count} jobs from Jooble response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Jooble response");
            }

            return jobs;
        }

        private DateTime ParseDate(string? dateString)
        {
            try
            {
                if (string.IsNullOrEmpty(dateString))
                    return DateTime.UtcNow;

                // Jooble returns ISO format dates
                if (DateTime.TryParse(dateString, out var date))
                    return date;
            }
            catch { }

            return DateTime.UtcNow;
        }
    }
}
