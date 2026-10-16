using Microsoft.EntityFrameworkCore;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using System.Text.Json;

namespace QuickHired.API.Services
{
    public interface IAnalyticsService
    {
        Task<DailyDigestData> GetDailyDigestDataAsync(DateTime date);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;

        public AnalyticsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DailyDigestData> GetDailyDigestDataAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Get daily signups - using EmailConfirmed field as proxy for registration date
            var dailySignups = await _context.AppUsers
                .Where(u => u.EmailConfirmed == true)
                .CountAsync();

            // Alternative: if you have a CreatedDate field, use that instead
            // var dailySignups = await _context.AppUsers
            //     .Where(u => u.CreatedDate >= startOfDay && u.CreatedDate <= endOfDay)
            //     .CountAsync();

            // Get total users
            var totalUsers = await _context.AppUsers.CountAsync();

            // Get active subscriptions
            var activeSubscriptions = await _context.UserSubscriptions
                .Where(s => s.Status == true)
                .CountAsync();

            // Get daily subscription signups
            var dailySubscriptions = await _context.UserSubscriptions
                .Where(s => s.CreatedAt >= startOfDay && s.CreatedAt <= endOfDay)
                .CountAsync();

            // Get country breakdown
            var countryStats = await _context.AppUsers
                .Where(u => !string.IsNullOrEmpty(u.Country))
                .GroupBy(u => u.Country)
                .Select(g => new CountryStatsDto
                {
                    Country = g.Key,
                    UserCount = g.Count()
                })
                .OrderByDescending(c => c.UserCount)
                .Take(10)
                .ToListAsync();

            // Get new countries today - simplified for now
            var newCountriesToday = await _context.AppUsers
                .Where(u => !string.IsNullOrEmpty(u.Country))
                .Select(u => u.Country)
                .Distinct()
                .Take(5)
                .ToListAsync();

            // Get meetings scheduled today
            var meetingsToday = await _context.Meetings
                .Where(m => m.StartTime >= startOfDay && m.StartTime <= endOfDay)
                .CountAsync();

            // Get top skills
            var skillsData = await _context.AppUsers
                .Where(u => !string.IsNullOrEmpty(u.Skills))
                .Select(u => u.Skills)
                .ToListAsync();

            var topSkills = ExtractTopSkills(skillsData);

            // Get experience level breakdown
            var experienceLevels = await _context.AppUsers
                .Where(u => !string.IsNullOrEmpty(u.ExperienceLevel))
                .GroupBy(u => u.ExperienceLevel)
                .Select(g => new ExperienceLevelDto
                {
                    Level = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // Get subscription plan breakdown
            var subscriptionPlans = await _context.UserSubscriptions
                .Where(s => s.Status == true)
                .GroupBy(s => s.PlanName)
                .Select(g => new SubscriptionPlanDto
                {
                    PlanName = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return new DailyDigestData
            {
                Date = date,
                DailySignups = dailySignups,
                TotalUsers = totalUsers,
                ActiveSubscriptions = activeSubscriptions,
                DailySubscriptions = dailySubscriptions,
                MeetingsScheduled = meetingsToday,
                CountryStats = countryStats,
                NewCountries = newCountriesToday,
                TopSkills = topSkills,
                ExperienceLevels = experienceLevels,
                SubscriptionPlans = subscriptionPlans,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private List<SkillStatsDto> ExtractTopSkills(List<string> skillsData)
        {
            var skillCounts = new Dictionary<string, int>();

            foreach (var skillSet in skillsData)
            {
                if (string.IsNullOrEmpty(skillSet)) continue;

                var skills = skillSet.Split(',', ';', '|')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s));

                foreach (var skill in skills)
                {
                    var cleanSkill = skill.ToLowerInvariant();
                    skillCounts[cleanSkill] = skillCounts.GetValueOrDefault(cleanSkill, 0) + 1;
                }
            }

            return skillCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new SkillStatsDto
                {
                    Skill = kvp.Key,
                    Count = kvp.Value
                })
                .ToList();
        }
    }

    // DTOs for the digest data
    public class DailyDigestData
    {
        public DateTime Date { get; set; }
        public int DailySignups { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int DailySubscriptions { get; set; }
        public int MeetingsScheduled { get; set; }
        public List<CountryStatsDto> CountryStats { get; set; } = new();
        public List<string> NewCountries { get; set; } = new();
        public List<SkillStatsDto> TopSkills { get; set; } = new();
        public List<ExperienceLevelDto> ExperienceLevels { get; set; } = new();
        public List<SubscriptionPlanDto> SubscriptionPlans { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class CountryStatsDto
    {
        public string Country { get; set; }
        public int UserCount { get; set; }
    }

    public class SkillStatsDto
    {
        public string Skill { get; set; }
        public int Count { get; set; }
    }

    public class ExperienceLevelDto
    {
        public string Level { get; set; }
        public int Count { get; set; }
    }

    public class SubscriptionPlanDto
    {
        public string PlanName { get; set; }
        public int Count { get; set; }
    }
}