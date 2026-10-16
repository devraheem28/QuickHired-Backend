using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickHired.API.DTOs;
using QuickHired.API.Services;
using QuickHired.BOL;
using QuickHired.DAL.Data;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MeetingRecordController : ControllerHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIInterviewController> _logger;
        private readonly string _openAiApiKey;
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _env;

        public MeetingRecordController(IHttpClientFactory httpClientFactory, IWebHostEnvironment env,
                                     IConfiguration configuration, ILogger<AIInterviewController> logger,
                                     AppDbContext dbContext)
        {
            _httpClient = httpClientFactory.CreateClient();
            _openAiApiKey = configuration["OpenAI:ApiKey"];
            _logger = logger;
            _dbContext = dbContext;
            _env = env;

        }
        [HttpPost("save-recording")]
        public async Task<IActionResult> SaveMeetingRecording([FromBody] MeetingRecordDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CompanyName) || string.IsNullOrWhiteSpace(dto.JobTitle))
                return BadRequest("Company name and job title are required.");

            var record = new MeetingRecord
            {
                UserId = dto.UserId,
                CompanyName = dto.CompanyName,
                JobTitle = dto.JobTitle,
                Summary = dto.Summary,
                Duration = dto.Duration,
                CreatedAt = dto.CreatedAt,
                SummaryFileName = dto.SummaryFileName,
                SummaryFileContent = Convert.FromBase64String(dto.SummaryFileBase64 ?? ""),
                TranscriptFileName = dto.TranscriptFileName,
                TranscriptFileContent = Convert.FromBase64String(dto.TranscriptFileBase64 ?? "")
            };

            _dbContext.MeetingRecords.Add(record);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Record saved" });
        }
        [HttpGet("get-records")]
        public async Task<IActionResult> GetMeetingsByEmail([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");
            var meetings = await _dbContext.MeetingRecords
                .Where(m => m.UserId == email)
                .ToListAsync();
            if (meetings == null || !meetings.Any())
                return NotFound("No meetings found for the given email.");

            var meetingRecordsDTO = meetings.Select(m => new
            {
                m.CompanyName,
                m.Summary,
                m.Duration,
                m.CreatedAt,
                m.SummaryFileName,
                SummaryFileBase64 = m.SummaryFileContent != null ? Convert.ToBase64String(m.SummaryFileContent) : null,
                m.TranscriptFileName,
                TranscriptFileBase64 = m.TranscriptFileContent != null
                ? Convert.ToBase64String(m.TranscriptFileContent)
                : null
            }).ToList();

            return Ok(meetingRecordsDTO);
        }

        [HttpGet("download-summary/{id}")]
        public IActionResult DownloadSummary(int id)
        {
            var record = _dbContext.MeetingRecords.FirstOrDefault(r => r.Id == id);
            if (record == null || record.SummaryFileContent == null)
                return NotFound("Summary not found.");

            return File(record.SummaryFileContent, "text/plain", record.SummaryFileName);
        }

    }
}

