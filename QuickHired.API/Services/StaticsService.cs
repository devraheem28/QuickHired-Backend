using QuickHired.BOL;
using QuickHired.DAL;
using QuickHired.DAL.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace QuickHired.API.Services
{
    /// <summary>
    /// Service for gathering daily application statistics for digest emails
    /// </summary>
    public interface IStatisticsService
    {
        Task<DailyDigestData> GetDailyDigestDataAsync(DateTime date);
    }

    public class StatisticsService : IStatisticsService
    {
        private readonly AppDbContext _context;

        public StatisticsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DailyDigestData> GetDailyDigestDataAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1).AddTicks(-1);
            var startOfMonth = new DateTime(date.Year, date.Month, 1);

            var digestData = new DailyDigestData
            {
                Date = date,
                GeneratedAt = DateTime.Now,

                // User Statistics
                TotalUsers = await _context.AppUsers.CountAsync(),
                // CreatedAt not present on AppUsers; using placeholder for now
                DailySignups = 0,

                // Subscription Statistics
                ActiveSubscriptions = await _context.UserSubscriptions
                    .Where(s => s.Status == true)
                    .CountAsync(),
                DailySubscriptions = await _context.UserSubscriptions
                    .Where(s => s.CreatedAt >= startOfDay && s.CreatedAt <= endOfDay)
                    .CountAsync(),

                // Meeting Statistics: use StartTime to approximate meetings scheduled that day
                MeetingsScheduled = await _context.Meetings
                    .Where(m => m.StartTime >= startOfDay && m.StartTime <= endOfDay)
                    .CountAsync(),

                // Country Statistics
                CountryStats = await GetCountryStatsAsync(),

                // New countries today (placeholder - would need location tracking)
                // Not available in current schema

                // Top skills (placeholder - would need skills data)

                // Experience levels (placeholder - would need experience level data)  

                // Subscription Plans
                SubscriptionPlans = await GetSubscriptionPlansAsync()
            };

            return digestData;
        }

        private async Task<List<CountryStatsDto>> GetCountryStatsAsync()
        {
            try
            {
                // Get top countries from user locations
                var countries = await _context.AppUsers
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

                return countries;
            }
            catch
            {
                return new List<CountryStatsDto>();
            }
        }

        private async Task<List<SubscriptionPlanDto>> GetSubscriptionPlansAsync()
        {
            try
            {
                var plans = await _context.UserSubscriptions
                    .Where(s => s.Status == true)
                    .GroupBy(s => s.PlanName)
                    .Select(g => new SubscriptionPlanDto
                    {
                        PlanName = g.Key ?? "Unknown",
                        Count = g.Count()
                    })
                    .OrderByDescending(p => p.Count)
                    .ToListAsync();

                return plans;
            }
            catch
            {
                return new List<SubscriptionPlanDto>();
            }
        }
    }
}