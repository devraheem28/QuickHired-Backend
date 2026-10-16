using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuickHired.API.DTOs;
using QuickHired.API.Services;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SendGrid.Helpers.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using System;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InterviewReportsController : ControllerHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIInterviewController> _logger;
        private readonly string _openAiApiKey;
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly ScrapingService _scrapingService;
        public InterviewReportsController(IHttpClientFactory httpClientFactory, IWebHostEnvironment env,
                                     IConfiguration configuration, ILogger<AIInterviewController> logger,
                                     AppDbContext dbContext, ScrapingService scrapingService)
        {
            _httpClient = httpClientFactory.CreateClient();
            _openAiApiKey = configuration["OpenAI:ApiKey"];
            _logger = logger;
            _dbContext = dbContext;
            _env = env;
            _scrapingService = scrapingService;
        }
        [HttpPost("SaveReport")]
        public async Task<IActionResult> SaveReport([FromBody] SaveReportDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest("Email is required");

            if (dto.Questions == null || dto.Questions.Count == 0)
                return BadRequest("At least one question is required");

            var userPersona = _dbContext.Personas.Where(p => dto.Email == p.UserId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();
            // Keep SaveReport lightweight: avoid GPT calls here for speed.
            // Accuracy and feedback will be generated in a separate step.
            double accuracy = 0;
            string overallImprovements = string.Empty;
            string overallWeaknesses = string.Empty;

            byte[]? transcriptBytes = null;
            if (!string.IsNullOrWhiteSpace(dto.TranscriptFileBase64))
            {
                try
                {
                    transcriptBytes = Convert.FromBase64String(dto.TranscriptFileBase64);
                }
                catch
                {
                    return BadRequest("TranscriptFileBase64 is not valid base64");
                }
            }

            var report = new InterviewReport
            {
                UserId = dto.Email,
                PersonaId = userPersona?.Id ?? 0,
                Duration = dto.DurationInSeconds,
                TotalQuestions = dto.TotalQuestions,
                ScorePercentage = Math.Round(accuracy, 2),
                InterviewType = dto.InterviewType,
                TranscriptFileName = dto.TranscriptFileName,
                TranscriptFileContent = transcriptBytes,
                Improvements = overallImprovements,
                Weaknesses = overallWeaknesses
            };


            _dbContext.InterviewReports.Add(report);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Report saved", reportId = report.Id });
        }
        [HttpPost("GetUserReportsByEmail")]
        public async Task<IActionResult> GetUserReportsByEmail([FromBody] EmailDTO model)
        {
            var reports = await _dbContext.InterviewReports
                .Where(r => r.UserId == model.Email)
                .ToListAsync();

            var personaMap = await _dbContext.Personas
                .Where(p => reports.Select(r => r.PersonaId).Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            //var result = reports.Select(r => new
            //{
            //    r.Id,
            //    r.InterviewType,
            //    r.Duration,
            //    r.ScorePercentage,
            //    r.CreatedAt,
            //    TranscriptFileBase64 = r.TranscriptFileContent != null ? Convert.ToBase64String(r.TranscriptFileContent) : null,
            //    r.TranscriptFileName,

            //    Persona = personaMap.TryGetValue(r.PersonaId, out var p) ? new
            //    {
            //        p.CompanyName,
            //        p.InterviewDomain,
            //        p.JobTitle
            //    } : null
            //});

            var result = reports.Select(r => new
            {
                r.Id,
                r.InterviewType,
                r.Duration,
                r.ScorePercentage,
                r.CreatedAt,
                TranscriptFileBase64 = r.TranscriptFileContent != null ? Convert.ToBase64String(r.TranscriptFileContent) : null,
                r.TranscriptFileName,
                r.Improvements,
                r.Weaknesses,
                Persona = personaMap.TryGetValue(r.PersonaId, out var p) ? new
                {
                    p.CompanyName,
                    p.InterviewDomain,
                    p.JobTitle
                } : null
            });


            return Ok(result);
        }

        private async Task<double> EvaluateAccuracy(string question, string answer)
        {
            var prompt = $@"
                            You are an evaluator for interview answers.
                            Question: {question}
                            Answer: {answer}
                            Rate how well the answer matches the question on a scale from 0 to 100.
                            Only return the number, no explanation.";

            var requestBody = new
            {
                model = "gpt-4o",
                messages = new[]
                {
                new { role = "system", content = "You are a helpful assistant that evaluates answers." },
                new { role = "user", content = prompt }
            },
                max_tokens = 150
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI Evaluation API Error: {0}", responseString);
                return 0;
            }

            var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);
            var rawScore = responseObject?.Choices?[0]?.Message?.Content?.Trim();

            if (double.TryParse(rawScore, out double score))
            {
                return Math.Clamp(score, 0, 100);
            }

            return 0;
        }
        public class QuestionAnswerPair
        {
            public string Question { get; set; }
            public string Answer { get; set; }
        }

        private async Task<(string improvements, string weaknesses)> EvaluateOverallFeedback(List<QuestionAnswerPair> qaPairs)
        {
            var sb = new StringBuilder();
            foreach (var qa in qaPairs)
            {
                sb.AppendLine($"Q: {qa.Question}\nA: {qa.Answer}\n");
            }
            var prompt = $@"
                You are a senior interview coach analyzing a candidate's performance.

                Below are several interview questions and their answers:

                {sb.ToString()}

                Please analyze all of the answers **as a whole** and provide:

                1. Top improvements the candidate should focus on.
                2. Main weaknesses or common issues across the answers.

                Respond in this JSON format:
                {{
                  ""improvements"": ""<string>"",
                  ""weaknesses"": ""<string>""
                }}";

            var requestBody = new
            {
                model = "gpt-4o",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that evaluates interview performance." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 400
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI Feedback Evaluation Error: {0}", responseString);
                return ("", "");
            }

            try
            {
                var root = JsonDocument.Parse(responseString);
                var contentText = root
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();


                var jsonMatch = Regex.Match(contentText, @"\{[\s\S]*?\}");
                if (jsonMatch.Success)
                {
                    var parsed = JsonSerializer.Deserialize<EvaluationResult>(jsonMatch.Value, options);
                    return (parsed?.Improvements ?? "", parsed?.Weaknesses ?? "");
                }
                else
                {
                    _logger.LogWarning("Could not extract JSON from GPT response: " + contentText);
                    return ("", "");
                }

            }
            catch
            {
                return ("", "");
            }
        }

        public class EvaluationResult
        {
            public double Score { get; set; }
            public string Improvements { get; set; }
            public string Weaknesses { get; set; }
        }


        private async Task<string> CallOpenAi(string prompt, int maxTokens = 500, string model = "gpt-4o")
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
            new { role = "system", content = "You are a senior interview analyst." },
            new { role = "user", content = prompt }
        },
                max_tokens = maxTokens
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API Error: {0}", responseString);
                return string.Empty;
            }

            var root = JsonDocument.Parse(responseString);
            return root.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString();
        }

        public class SummaryResult
        {
            [JsonPropertyName("analysis")] public List<string> Analysis { get; set; } = new();
            [JsonPropertyName("improvements")] public List<string> Improvements { get; set; } = new();
            [JsonPropertyName("weaknesses")] public List<string> Weaknesses { get; set; } = new();
        }

        [HttpPost("GenerateSummary")]
        public async Task<IActionResult> GenerateSummary([FromBody] InterviewReportDto report)
        {
            var conversation = report.Questions
                .Select((q, i) => $"Q{i + 1}: {q.Question}\nA{i + 1}: {q.Answer}")
                .ToList();

            var prompt = $@"
                                Analyze the following mock interview and return JSON with three arrays: 'analysis', 'improvements', and 'weaknesses'.

                                Interview:
                                {string.Join("\n\n", conversation)}

                                Respond ONLY with JSON in this format:
                                {{
                                    ""analysis"": [""Key insight 1"", ""Key insight 2""],
                                    ""improvements"": [""Improvement 1"", ""Improvement 2""],
                                    ""weaknesses"": [""Weakness 1"", ""Weakness 2""]
                                }}";

            // Use a faster model for summary generation
            var gptResponse = await CallOpenAi(prompt, maxTokens: 600, model: "gpt-4o-mini");

            if (string.IsNullOrWhiteSpace(gptResponse))
                return StatusCode(500, "GPT returned empty response");

            try
            {
                var jsonMatch = Regex.Match(gptResponse, @"\{[\s\S]*?\}");
                if (!jsonMatch.Success)
                {
                    _logger.LogWarning("GPT response did not contain valid JSON: " + gptResponse);
                    return BadRequest("Invalid GPT summary format");
                }

                var parsed = JsonSerializer.Deserialize<SummaryResult>(jsonMatch.Value);
                return Ok(new
                {
                    analysis = parsed?.Analysis ?? new List<string>(),
                    improvements = parsed?.Improvements ?? new List<string>(),
                    weaknesses = parsed?.Weaknesses ?? new List<string>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GPT summary JSON");
                return StatusCode(500, "Failed to parse GPT summary.");
            }
        }

        public class UpdateFeedbackDTO
        {
            public int ReportId { get; set; }
            public string Improvements { get; set; } = string.Empty;
            public string Weaknesses { get; set; } = string.Empty;
        }

        [HttpPost("UpdateReportFeedback")]
        public async Task<IActionResult> UpdateReportFeedback([FromBody] UpdateFeedbackDTO dto)
        {
            var report = await _dbContext.InterviewReports.FirstOrDefaultAsync(r => r.Id == dto.ReportId);
            if (report == null)
                return NotFound("Report not found");

            report.Improvements = dto.Improvements;
            report.Weaknesses = dto.Weaknesses;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Report feedback updated" });
        }

        [HttpPost("GenerateAnalysis")]
        public async Task<IActionResult> GenerateAnalysis([FromBody] InterviewReportDto report)
        {
            var conversation = report.Questions
                .Select((q, i) => $"Q{i + 1}: {q.Question}\nA{i + 1}: {q.Answer}")
                .ToList();

            var prompt = $"Analyze the following mock interview. Provide 4-5 key insights in list format:\n\n{string.Join("\n\n", conversation)}";

            var analysisText = await CallOpenAi(prompt);

            if (string.IsNullOrWhiteSpace(analysisText))
                return StatusCode(500, "GPT returned empty response");

            var analysisList = analysisText.Split('\n')
                                           .Where(x => !string.IsNullOrWhiteSpace(x))
                                           .Select(x => x.TrimStart('-', '*', ' ').Trim())
                                           .ToList();

            return Ok(new { analysis = analysisList });
        }

        [HttpPost("GenerateFeedback")]
        public async Task<IActionResult> GenerateFeedback([FromBody] InterviewReportDto report)
        {
            var conversation = report.Questions
                .Select((q, i) => $"Q{i + 1}: {q.Question}\nA{i + 1}: {q.Answer}")
                .ToList();

            var prompt = $@"
                    Below is a mock interview with questions and answers:

                    {string.Join("\n\n", conversation)}

                    Please provide feedback in this JSON format:

                    {{
                      ""improvements"": [""First improvement"", ""Second improvement"", ""...""] ,
                      ""weaknesses"": [""First weakness"", ""Second weakness"", ""...""]
                    }}";

            var gptResponse = await CallOpenAi(prompt);

            if (string.IsNullOrWhiteSpace(gptResponse))
                return StatusCode(500, "GPT returned empty response");

            try
            {
                var jsonMatch = Regex.Match(gptResponse, @"\{[\s\S]*?\}");
                if (!jsonMatch.Success)
                {
                    _logger.LogWarning("GPT response did not contain valid JSON: " + gptResponse);
                    return BadRequest("Invalid GPT feedback format");
                }

                var parsed = JsonSerializer.Deserialize<EvaluationListResult>(jsonMatch.Value);
                return Ok(new
                {
                    improvements = parsed?.Improvements ?? new List<string>(),
                    weaknesses = parsed?.Weaknesses ?? new List<string>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing GPT feedback JSON");
                return StatusCode(500, "Failed to parse GPT feedback.");
            }
        }


        public class EvaluationListResult
        {
            [JsonPropertyName("improvements")]
            public List<string> Improvements { get; set; }

            [JsonPropertyName("weaknesses")]
            public List<string> Weaknesses { get; set; }
        }

        public class InterviewReportDto
        {
            public string Email { get; set; }
            public int TotalQuestions { get; set; }
            public List<QuestionAnswerDto> Questions { get; set; }
            public string InterviewType { get; set; }
        }
        public class QuestionAnswerDto
        {
            public string Question { get; set; }
            public string Answer { get; set; }
        }

        public class ThemeRequest
        {
            public string Email { get; set; }
            public string Theme { get; set; }
        }

        [HttpPost("SetTheme")]
        public async Task<IActionResult> SetTheme([FromBody] ThemeRequest request)
        {
            var preference = new UserThemePreference
            {
                Email = request.Email,
                Theme = request.Theme,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.UserThemePreferences.Add(preference);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "New theme preference saved." });
        }

        [HttpGet("GetTheme/{email}")]
        public async Task<IActionResult> GetTheme(string email)
        {
            //var preference = await _dbContext.UserThemePreferences
            //    .FirstOrDefaultAsync(p => p.Email == email);
            var preference = await _dbContext.UserThemePreferences
            .Where(p => p.Email == email)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

            var theme = preference?.Theme ?? "light";
            return Ok(theme);
        }
        public class EmailDTO
        {
            public string Email { get; set; }
        }
    }
}
