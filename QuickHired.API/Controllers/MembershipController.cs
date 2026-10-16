using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using QuickHired.API.Helpers;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using System.Text.RegularExpressions;
using static QuickHired.API.Controllers.SecurityMasterController;
using static QuickHired.API.Controllers.StripeController;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembershipController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUsers> _userManager;
        private readonly ILogger<MembershipController> _logger;

        public MembershipController(AppDbContext context, UserManager<AppUsers> userManager, ILogger<MembershipController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }
        [HttpGet("user-subscriptions")]
        public async Task<IActionResult> GetUserSubscriptions([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest("Email is required.");

            var subscriptions = await _context.UserSubscriptions
                .Where(s => s.UserId == email && s.Status)
                .Select(s => new SubscriptionDto
                {
                    PlanName = s.PlanName,
                    CreatedAt = s.CreatedAt,
                    CustomerId = s.CustomerId,
                    SubscriptionId = s.SubscriptionKey
                })
                .ToListAsync();

            return Ok(subscriptions);
        }
        [HttpPost("cancel-subscription-local")]
        public async Task<IActionResult> CancelSubscriptionLocal([FromBody] CancelSubscriptionRequest request)
        {
            try
            {
                var subscription = await _context.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == request.Email && s.SubscriptionKey == null);

                if (subscription != null)
                {
                    subscription.Status = false;
                    subscription.CancellationReason = request.CancellationReason;
                    subscription.CancelledDate = DateTime.UtcNow;

                    _context.Entry(subscription).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                }
                var user = await _userManager.FindByIdAsync(request.Email);
                EmailHelper.SendCancelMembershipEmail(user.Email, user.FirstName);
                if (user != null)
                {
                    await _userManager.DeleteAsync(user);
                }
                return Ok(new { success = true, message = "Membership canceled successfully." });
            }
            catch (Exception ex)
            {
                LoggingHelper.LogError("Payment_Subscriptions", "CANCEL_MEMBERSHIP",
                    $"Failed to cancel membership for user email: {request.Email}", ex, _logger);
                return StatusCode(500, new { success = false, error = "Internal server error." });
            }
        }
        [HttpGet("user-plan/{userId}")]
        public async Task<IActionResult> GetUserSubscriptionPlan(string userId)
        {
            var userSubscription = await _context.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId);

            if (userSubscription == null)
                return NotFound("User subscription not found");

            var planName = userSubscription.PlanName;

            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionName == planName);

            if (subscription == null)
                return NotFound("Subscription plan details not found");

            string totalSessions = ExtractFirstNumberOrText(subscription.AIInterviewCopilot);
            string totalSessionsMock = ExtractFirstNumberOrText(subscription.AIMockInterview);
            string sessionLength = ExtractFirstNumberOrText(subscription.SessionLength);
            string interviewReports = ExtractFirstNumberOrText(subscription.AIInterviewReports);
            int recordedMeetings = subscription.MeetingRecording;
            return Ok(new
            {
                totalSessions,
                sessionLength,
                interviewReports,
                recordedMeetings,
                totalSessionsMock
            });
        }
        private string ExtractFirstNumberOrText(string input)
        {
            if (string.IsNullOrEmpty(input)) return "N/A";

            var match = Regex.Match(input, @"\d+");
            return match.Success ? match.Value : input;
        }
        [HttpGet("get-plan-features/{planName}")]
        public async Task<IActionResult> GetPlanFeatures(string planName)
        {
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.SubscriptionName == planName);
            if (subscription == null)
                return NotFound("Subscription plan details not found");

            string totalSessions = ExtractFirstNumberOrText(subscription.AIInterviewCopilot);
            string totalSessionsMock = ExtractFirstNumberOrText(subscription.AIMockInterview);
            string sessionLength = ExtractFirstNumberOrText(subscription.SessionLength);
            string interviewReports = ExtractFirstNumberOrText(subscription.AIInterviewReports);
            int recordedMeetings = subscription.MeetingRecording;
            decimal price = subscription.Price;
            return Ok(new
            {
                totalSessions,
                sessionLength,
                interviewReports,
                recordedMeetings,
                totalSessionsMock,
                price
            });
        }
        [HttpGet("user-sessions/{userId}")]
        public async Task<IActionResult> GetUserSessions(string userId)
        {
            var now = DateTime.UtcNow;

            var sessions = await _context.Personas
                .Where(p => p.UserId == userId &&
                            p.CreatedAt.Month == now.Month &&
                            p.CreatedAt.Year == now.Year)
                .Select(p => new
                {
                    p.Id,
                    p.InterviewType,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("user-reports/{userId}")]
        public async Task<IActionResult> GetUserReports(string userId)
        {
            var now = DateTime.UtcNow;

            var sessions = await _context.InterviewReports
                .Where(p => p.UserId == userId &&
                            p.CreatedAt.Month == now.Month &&
                            p.CreatedAt.Year == now.Year)
                .Select(p => new
                {
                    p.Id,
                    p.InterviewType,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("user-meetings/{userId}")]
        public async Task<IActionResult> GetUserMeetings(string userId)
        {
            var now = DateTime.UtcNow;

            var sessions = await _context.MeetingRecords
                .Where(p => p.UserId == userId &&
                            p.CreatedAt.Month == now.Month &&
                            p.CreatedAt.Year == now.Year)
                .Select(p => new
                {
                    p.Id,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("get-all-subscriptions")]
        public async Task<IActionResult> GetAllSubscriptions()
        {
            var subscriptions = await _context.Subscriptions.ToListAsync();
            return Ok(subscriptions);
        }
        [HttpPut("update-subscription/{id}")]
        public async Task<IActionResult> UpdateSubscription(int id, [FromBody] Subscriptions updatedSub)
        {
            var existing = await _context.Subscriptions.FindAsync(id);
            if (existing == null)
                return NotFound();

            // Update the properties
            existing.SubscriptionName = updatedSub.SubscriptionName;
            existing.AIInterviewCopilot = updatedSub.AIInterviewCopilot;
            existing.SessionLength = updatedSub.SessionLength;
            existing.AIMockInterview = updatedSub.AIMockInterview;
            existing.AIInterviewReports = updatedSub.AIInterviewReports;
            existing.MeetingRecording = updatedSub.MeetingRecording;
            existing.Price = updatedSub.Price;

            _context.Entry(existing).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(existing);
        }

        [HttpGet("get-all-user-subscriptions")]
        public async Task<IActionResult> GetAllUserSubscriptions()
        {
            var userSubscriptions = await _context.UserSubscriptions.ToListAsync();
            var result = new List<UserSubscriptionDto>();

            foreach (var sub in userSubscriptions)
            {
                var user = await _userManager.FindByIdAsync(sub.UserId);

                result.Add(new UserSubscriptionDto
                {
                    UserName = $"{user?.FirstName} {user?.LastName}",
                    Email = user?.Email,
                    PlanName = sub.PlanName,
                    StartDate = sub.CreatedAt,
                    Status = sub.Status ? "Active" : "Cancelled",
                    CancellationReason = string.IsNullOrEmpty(sub.CancellationReason) ? "-" : sub.CancellationReason,
                    CancelledDate = sub.CancelledDate.HasValue
                        ? sub.CancelledDate.Value.ToString("yyyy-MM-dd")
                        : "-"
                });
            }

            return Ok(result);
        }
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateReport([FromBody] ReportRequest request)
        {
            try
            {
                var usersQuery = _context.UserSubscriptions
                    .Where(u => u.CreatedAt.Year == request.Year && u.CreatedAt.Month == request.Month);

                if (!string.Equals(request.SubscriptionName, "All", StringComparison.OrdinalIgnoreCase))
                {
                    usersQuery = usersQuery.Where(u => u.PlanName == request.SubscriptionName);
                }

                var userSubscriptions = await usersQuery.ToListAsync();
                var subscriptions = await _context.Subscriptions.ToListAsync();

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();

                var worksheet = package.Workbook.Worksheets.Add("Monthly Report");

                worksheet.Cells[1, 1].Value = "User Name";
                worksheet.Cells[1, 2].Value = "Subscription Name";
                worksheet.Cells[1, 3].Value = "Created Date";
                worksheet.Cells[1, 4].Value = "Subscription Price";

                int row = 2;
                decimal totalRevenue = 0;
                int totalUsers = 0;

                foreach (var userSub in userSubscriptions)
                {
                    var applicationUser = await _userManager.FindByIdAsync(userSub.UserId);
                    string fullName = applicationUser != null ? $"{applicationUser.FirstName} {applicationUser.LastName}" : "Unknown";

                    var subscription = subscriptions.FirstOrDefault(s => s.SubscriptionName == userSub.PlanName);
                    decimal price = subscription?.Price ?? 0;

                    worksheet.Cells[row, 1].Value = fullName;
                    worksheet.Cells[row, 2].Value = userSub.PlanName;
                    worksheet.Cells[row, 3].Value = userSub.CreatedAt.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 4].Value = price;

                    totalRevenue += price;
                    totalUsers++;
                    row++;
                }

                worksheet.Cells[row + 1, 3].Value = "Total Users:";
                worksheet.Cells[row + 1, 4].Value = totalUsers;

                worksheet.Cells[row + 2, 3].Value = "Total Revenue:";
                worksheet.Cells[row + 2, 4].Value = totalRevenue;

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Monthly_Report_{request.Year}_{request.Month}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {

                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        public class ReportRequest
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public string SubscriptionName { get; set; }
        }
        public class UserSubscriptionDto
        {
            public string UserName { get; set; }
            public string Email { get; set; }
            public string PlanName { get; set; }
            public DateTime StartDate { get; set; }
            public string Status { get; set; }
            public string CancellationReason { get; set; }
            public string CancelledDate { get; set; }
        }

    }
}
