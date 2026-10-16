using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickHired.API.Services;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Ensure only authenticated admins can access
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IConfiguration _configuration;

        public AnalyticsController(IAnalyticsService analyticsService, IConfiguration configuration)
        {
            _analyticsService = analyticsService;
            _configuration = configuration;
        }

        /// <summary>
        /// Get daily digest data for a specific date
        /// </summary>
        /// <param name="date">Date in yyyy-MM-dd format (defaults to yesterday)</param>
        /// <returns>Daily digest analytics data</returns>
        [HttpGet("daily-digest")]
        public async Task<IActionResult> GetDailyDigest([FromQuery] string? date = null)
        {
            try
            {
                DateTime targetDate;
                if (string.IsNullOrEmpty(date))
                {
                    targetDate = DateTime.Now.AddDays(-1);
                }
                else if (!DateTime.TryParse(date, out targetDate))
                {
                    return BadRequest("Invalid date format. Use yyyy-MM-dd.");
                }

                var digestData = await _analyticsService.GetDailyDigestDataAsync(targetDate);
                return Ok(digestData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating analytics: {ex.Message}");
            }
        }

        /// <summary>
        /// Manually trigger daily digest email
        /// </summary>
        /// <param name="adminEmail">Email address to send digest to</param>
        /// <param name="date">Date for digest (optional, defaults to yesterday)</param>
        /// <returns>Success/failure status</returns>
        [HttpPost("send-digest")]
        public async Task<IActionResult> SendDigestEmail([FromBody] SendDigestRequest request)
        {
            try
            {
                DateTime targetDate = string.IsNullOrEmpty(request.Date)
                    ? DateTime.Now.AddDays(-1)
                    : DateTime.Parse(request.Date);

                var digestData = await _analyticsService.GetDailyDigestDataAsync(targetDate);
                var adminEmail = string.IsNullOrWhiteSpace(request.AdminEmail)
                    ? _configuration["AdminEmail"] ?? "mohsinrana09@yahoo.com"
                    : request.AdminEmail;

                await Helpers.EmailHelper.SendDailyDigestEmail(adminEmail, digestData);

                return Ok(new { success = true, message = "Daily digest email sent successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Failed to send digest: {ex.Message}" });
            }
        }
    }

    public class SendDigestRequest
    {
        public string AdminEmail { get; set; } = "";
        public string? Date { get; set; }
    }
}