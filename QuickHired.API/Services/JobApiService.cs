using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RestSharp;
using HtmlAgilityPack;
using QuickHired.API.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace QuickHired.API.Services
{
    /// <summary>
    /// Service for aggregating job listings from Jooble
    /// </summary>
    public class JobApiService
    {
        private readonly ILogger<JobApiService> _logger;
        private readonly IConfiguration _configuration;
        private readonly JoobleJobService _joobleService;

        public JobApiService(
            ILogger<JobApiService> logger,
            IConfiguration configuration,
            JoobleJobService joobleService)
        {
            _logger = logger;
            _configuration = configuration;
            _joobleService = joobleService;
        }

        /// <summary>
        /// Search jobs from Jooble API
        /// </summary>
        public async Task<List<JobListingDto>> SearchJobsAsync(
            string keywords,
            string location,
            string? jobType = null,
            decimal? salaryMin = null,
            decimal? salaryMax = null)
        {
            try
            {
                _logger.LogInformation($"Searching for jobs: {keywords} in {location}");

                var jobs = new List<JobListingDto>();

                // Search Jooble jobs (free API, no auth required)
                try
                {
                    var joobleJobs = await _joobleService.SearchJobsAsync(keywords, location, jobType);
                    jobs.AddRange(joobleJobs);
                    _logger.LogInformation($"Found {joobleJobs.Count} jobs on Jooble");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching Jooble jobs");
                }

                // Filter by salary if specified
                if (salaryMin.HasValue || salaryMax.HasValue)
                {
                    jobs = FilterBySalary(jobs, salaryMin, salaryMax).ToList();
                }

                _logger.LogInformation($"Total jobs found: {jobs.Count}");
                return jobs.OrderByDescending(j => j.PostedDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchJobsAsync");
                return new List<JobListingDto>();
            }
        }

        /// <summary>
        /// Filters jobs by salary range
        /// </summary>
        private IEnumerable<JobListingDto> FilterBySalary(IEnumerable<JobListingDto> jobs, decimal? min, decimal? max)
        {
            return jobs.Where(j =>
            {
                if (j.Salary == null) return false;
                if (!TryParseSalary(j.Salary, out var s)) return false;
                bool minOk = !min.HasValue || s.min >= min.Value;
                bool maxOk = !max.HasValue || s.max <= max.Value;
                return minOk && maxOk;
            });
        }

        // Helper to parse salary string to min/max decimal
        private bool TryParseSalary(string salaryStr, out (decimal min, decimal max) result)
        {
            result = (0, 0);
            if (string.IsNullOrWhiteSpace(salaryStr)) return false;
            var parts = salaryStr.Replace("$", "").Replace(",", "").Split('-');
            if (parts.Length == 2 && decimal.TryParse(parts[0], out var min) && decimal.TryParse(parts[1], out var max))
            {
                result = (min, max);
                return true;
            }
            if (parts.Length == 1 && decimal.TryParse(parts[0], out var val))
            {
                result = (val, val);
                return true;
            }
            return false;
        }
    }
}
