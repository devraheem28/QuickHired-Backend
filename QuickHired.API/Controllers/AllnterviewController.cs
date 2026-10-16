using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Hosting;
using QuickHired.BOL;
using QuickHired.API.DTOs;

using Microsoft.EntityFrameworkCore;
using QuickHired.API.Services;
using Stripe;
using Microsoft.AspNetCore.Http.HttpResults;
using QuickHired.API.Models;
using AutoMapper;
using System.Text.RegularExpressions;
using SendGrid.Helpers.Mail;
using SendGrid;
using QuickHired.API.Helpers;
using Microsoft.AspNetCore.Identity;
using QuickHired.DAL.Data;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIInterviewController : ControllerHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIInterviewController> _logger;
        private readonly string _openAiApiKey;
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly ScrapingService _scrapingService;
        private readonly IConfiguration configuration;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<AppUsers> _userManager;
        public AIInterviewController(IHttpClientFactory httpClientFactory, IWebHostEnvironment env
                                     , UserManager<AppUsers> userManager,
                                     IConfiguration configuration, ILogger<AIInterviewController> logger,
                                     AppDbContext dbContext, ScrapingService scrapingService,
                                     IHttpContextAccessor httpContextAccessor, IMapper mapper)
        {
            _httpClient = httpClientFactory.CreateClient();
            _openAiApiKey = configuration["OpenAI:ApiKey"];
            this.configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
            _userManager = userManager;
            _env = env;
            _scrapingService = scrapingService;
            _httpContextAccessor = httpContextAccessor;
            _mapper = mapper;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskAI([FromBody] AIQuestionModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Question))
            {
                return BadRequest(new { message = "Question cannot be empty." });
            }

            var response = await GetAIResponse(model.Question, model.Email);

            if (response == null)
            {
                return StatusCode(500, new { message = "Failed to get response from AI." });
            }
            return Ok(new { answer = response });
        }
        [HttpPost("simple-ask")]
        public async Task<IActionResult> SimpleAsk([FromBody] SimpleAIQuestionModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Question))
                return BadRequest(new { message = "Question cannot be empty." });

            var response = await GetAIResponse(model.Question);

            if (response == null)
                return StatusCode(500, new { message = "Failed to get response from AI." });

            return Ok(new { answer = response });
        }
        private async Task<string> GetAIMockResponse(string question)
        {
            try
            {
                // Escape newlines to fix JSON errors
                question = question.Replace("\n", "\\n").Replace("\r", "\\r");
                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "system", content = question },
                        new { role = "user", content = question }
                    },
                    max_tokens = 500
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                _logger.LogInformation("Sending request to OpenAI: {0}", JsonSerializer.Serialize(requestBody, options));

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                string responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response from OpenAI: {0}", responseString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API Error: {0}", responseString);
                    return null;
                }

                var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

                if (responseObject?.Choices != null && responseObject.Choices.Count > 0)
                {
                    return responseObject.Choices[0].Message.Content;
                }

                return "No response from AI.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return null;
            }
        }
        private async Task<string> GetAIResponse(string question, string userEmail)
        {
            try
            {
                // Escape newlines to fix JSON errors
                question = question.Replace("\n", "\\n").Replace("\r", "\\r");
                var persona = await GetUserPersonaByEmailAsync(userEmail);

                string prompt;
                if (persona != null)
                {
                    // Create a persona-based prompt
                    prompt = $@"
                                You are acting as a highly intelligent and professional candidate.
                                You are currently being interviewed for the position of {persona.JobTitle} at {persona.CompanyName}.

                                Company Description: {persona.CompanyDescription}
                                Job Description: {persona.JobDescription}
                                Interview Domain Focus: {persona.InterviewDomain}

                                Please answer the following interview question as the candidate, following these strict guidelines:

                                - Respond only in English
                                - Keep a professional, clear, and conversational tone
                                - Use short, direct paragraphs — one idea or fact per line
                                - Use bullet points (-) or numbered lists (1., 2., etc.) — only one point per line
                                - Avoid repeating the same idea, even in different words
                                - Do not repeat any points or phrases unnecessarily
                                - Do not use HTML, Markdown, or special formatting
                                - Use real-world examples where relevant to enhance your response

                                Interview Question: {question}
                                ";
                }
                else
                {
                    // Fallback to default prompt
                    prompt = $"You are an AI interview assistant. Please assist with the interview question.\n\nInterview Question: {question}";
                }

                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "system", content = prompt },
                        new { role = "user", content = question }
                    },
                    max_tokens = 800
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                _logger.LogInformation("Sending request to OpenAI: {0}", JsonSerializer.Serialize(requestBody, options));

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                string responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response from OpenAI: {0}", responseString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API Error: {0}", responseString);
                    return null;
                }

                var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

                if (responseObject?.Choices != null && responseObject.Choices.Count > 0)
                {
                    return responseObject.Choices[0].Message.Content;
                }

                return "No response from AI.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return null;
            }
        }

        [HttpPost("mock-question")]
        public async Task<IActionResult> GetMockQuestion()
        {
            try
            {
                var question = "Hello,Welcome to the Quick Hired mock interview.";
                var audioStream = await GenerateSpeechFromText(question);
                var fileName = $"question_{Guid.NewGuid()}.mp3";
                var audioPath = Path.Combine("wwwroot/audios", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(audioPath));
                using (var fs = new FileStream(audioPath, FileMode.Create, FileAccess.Write))
                {
                    await audioStream.CopyToAsync(fs);
                }
                //var audioUrl = $"https://localhost:44303/audios/{fileName}";
                //var avatarVideoUrl = $"https://localhost:44303/videos/created-from-canva.mp4";
                var domain = Request.Host.Value.Contains("localhost")
                ? "https://localhost:44303"
                : "https://api.quickhired.com";

                var audioUrl = $"{domain}/audios/{fileName}";
                var avatarVideoUrl = $"{domain}/videos/created-from-canva.mp4";

                if (string.IsNullOrEmpty(avatarVideoUrl))
                {
                    return StatusCode(500, new { message = "Failed to generate avatar video." });
                }

                return Ok(new
                {
                    questionText = question,
                    audioUrl = audioUrl,
                    avatarVideoUrl = avatarVideoUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating interview question");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
        [HttpPost("next-question")]
        public async Task<IActionResult> GetNextInterviewQuestion([FromBody] NextQuestionsDTO dto)
        {
            try
            {
                var persona = await GetUserPersonaByEmailAsync(dto.UserEmail);
                string questionPrompt =
                    $@"You are a highly intelligent and professional AI interview assistant.

                    The candidate is currently participating in a **mock interview** for the position of **{persona.JobTitle}** at **{persona.CompanyName}**.

                    ## Company Information
                    - **Company Name**: {persona.CompanyName}
                    - **Company Description**: {persona.CompanyDescription}

                    ## Job Information
                    - **Job Title**: {persona.JobTitle}
                    - **Job Description**: {persona.JobDescription}

                    ## Interview Domain
                    - This mock interview focuses on the domain of **{persona.InterviewDomain}**.

                    ## Your Role
                    - Simulate **one realistic interview question** tailored to the candidate’s role and domain.
                    - Ensure the question is:
                      - Clear
                      - Relevant
                      - Representative of real interview scenarios
                    - Vary the types of question between:
                      - **Technical**
                      - **Situational**
                      - **Behavioral** (using STAR format: Situation, Task, Action, Result)

                   ## Output Format
                    Respond strictly in the following JSON format, with no markdown or code block.

                    Based on the type of interview question, choose the most suitable mood (icon + text) from the predefined list below. Pick one that best matches the tone of the question.

                    ### Allowed Moods:
                    - {{ ""icon"": ""😊"", ""text"": ""Friendly and encouraging"" }}
                    - {{ ""icon"": ""🤔"", ""text"": ""Thoughtfully listening"" }}
                    - {{ ""icon"": ""👍"", ""text"": ""Impressed with response"" }}
                    - {{ ""icon"": ""😌"", ""text"": ""Relaxed and supportive"" }}
                    - {{ ""icon"": ""🎯"", ""text"": ""Focused on details"" }}
                    - {{ ""icon"": ""😬"", ""text"": ""Challenging and intense"" }}
                    - {{ ""icon"": ""😅"", ""text"": ""Nervous but curious"" }}

                    ### Format:
                    {{
                      ""questionText"": ""your generated interview question here..."",
                      ""mood"": {{
                        ""icon"": ""one of the above icons"",
                        ""text"": ""matching mood description""
                      }}
                    }}
                    ";

                var question = await GetAIMockResponse(questionPrompt);
                question = question.Trim();
                if (question.StartsWith("```json"))
                {
                    question = question.Substring(7); // remove "```json"
                }
                if (question.StartsWith("```"))
                {
                    question = question.Substring(3); // remove remaining ```
                }
                if (question.EndsWith("```"))
                {
                    question = question.Substring(0, question.Length - 3);
                }

                // Then parse it
                var parsedJson = JsonSerializer.Deserialize<QuestionWithMood>(question);
                if (parsedJson == null || string.IsNullOrWhiteSpace(parsedJson.QuestionText))
                {
                    return StatusCode(500, new { message = "Failed to parse question or mood." });
                }


                if (string.IsNullOrWhiteSpace(question))
                {
                    return StatusCode(500, new { message = "Failed to generate question." });
                }
                //var audioStream = await GenerateSpeechFromText(question);
                var audioStream = await GenerateSpeechFromText(parsedJson.QuestionText);

                var fileName = $"question_{Guid.NewGuid()}.mp3";
                var audioPath = Path.Combine("wwwroot/audios", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(audioPath));
                using (var fs = new FileStream(audioPath, FileMode.Create, FileAccess.Write))
                {
                    await audioStream.CopyToAsync(fs);
                }
                var domain = Request.Host.Value.Contains("localhost")
                 ? "https://localhost:44303"
                 : "https://api.quickhired.com";

                var audioUrl = $"{domain}/audios/{fileName}";
                var avatarVideoUrl = $"{domain}/videos/created-from-canva.mp4";

                if (string.IsNullOrEmpty(avatarVideoUrl))
                {
                    return StatusCode(500, new { message = "Failed to generate avatar video." });
                }
                //return Ok(new
                //{
                //    questionText = question,
                //    audioUrl = audioUrl,
                //    avatarVideoUrl = avatarVideoUrl
                //});
                return Ok(new
                {
                    questionText = parsedJson.QuestionText,
                    audioUrl = audioUrl,
                    avatarVideoUrl = avatarVideoUrl,
                    mood = parsedJson.Mood
                });



            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating interview question");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        public class TtsRequest
        {
            public string Text { get; set; }
        }

        [HttpPost("tts")]
        public async Task<IActionResult> GenerateTts([FromBody] TtsRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Text))
            {
                return BadRequest(new { message = "Text is required." });
            }

            try
            {
                var audioStream = await GenerateSpeechFromText(req.Text);
                if (audioStream == null)
                {
                    return StatusCode(500, new { message = "Failed to generate TTS audio." });
                }

                var fileName = $"tts_{Guid.NewGuid()}.mp3";
                var audioPath = Path.Combine("wwwroot/audios", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(audioPath));
                using (var fs = new FileStream(audioPath, FileMode.Create, FileAccess.Write))
                {
                    await audioStream.CopyToAsync(fs);
                }

                var domain = Request.Host.Value.Contains("localhost")
                    ? "https://localhost:44303"
                    : "https://api.quickhired.com";

                var audioUrl = $"{domain}/audios/{fileName}";
                return Ok(new { audioUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TTS audio");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        public class QuestionWithMood
        {
            [JsonPropertyName("questionText")]
            public string QuestionText { get; set; }

            [JsonPropertyName("mood")]
            public Mood Mood { get; set; }
        }

        public class Mood
        {
            [JsonPropertyName("icon")]
            public string Icon { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }
        }

        private async Task<string?> GenerateTalkingAvatarVideo(string scriptText)
        {
            var apiUrl = "https://api.d-id.com/talks";
            var apiKey = "Basic cHJvZXZlbnRpZnlAZ21haWwuY29t:8c64TaFLJ1IKfo_hftu-v";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("authorization", apiKey);

            var payload = new
            {
                source_url = "https://d-id-public-bucket.s3.us-west-2.amazonaws.com/alice.jpg",
                script = new
                {
                    type = "text",
                    input = "scriptText",
                    subtitles = "false",
                    provider = new
                    {
                        type = "microsoft",
                        voice_id = "Sara"
                    },
                    ssml = "false"
                },
                config = new
                {
                    fluent = "false",
                    background = "transparent",
                }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("POST FAILED: " + response.StatusCode);
                Console.WriteLine("Response: " + responseText);
                return null;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseText);
            var talkId = result.GetProperty("id").GetString();

            // Polling the video creation status
            var getUrl = $"https://api.d-id.com/talks/{talkId}";
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(3000);
                var pollResponse = await httpClient.GetAsync(getUrl);
                var pollText = await pollResponse.Content.ReadAsStringAsync();

                var pollData = JsonSerializer.Deserialize<JsonElement>(pollText);
                if (pollData.TryGetProperty("result_url", out var resultUrl))
                {
                    return resultUrl.GetString();
                }
            }
            Console.WriteLine("Video processing timed out.");
            return null;
        }
        [HttpGet("questions")]
        public async Task<IActionResult> GetInterviewQuestions(string company, string role)
        {
            //return Ok(new { message = "Persona created successfully!" });
            HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            if (string.IsNullOrEmpty(company) || string.IsNullOrEmpty(role))
                return BadRequest("Company and role are required.");

            var questions = await _scrapingService.GetInterviewQuestions(company, role);

            if (questions == null || !questions.Any())
                return NotFound("No questions found.");

            return Ok(questions);
        }

        //private async Task<Stream?> GenerateSpeechFromText(string text)
        //{
        //    var voice = configuration["OpenAI:TTSVoice"] ?? "shimmer";

        //    var requestContent = new
        //    {
        //        model = "gpt-4o-mini-tts",
        //        input = text,
        //        voice = voice
        //    };

        //    using var request = new HttpRequestMessage(
        //        HttpMethod.Post,
        //        "https://api.openai.com/v1/audio/speech"
        //    );

        //    request.Headers.Authorization =
        //        new AuthenticationHeaderValue("Bearer", _openAiApiKey);

        //    request.Content = new StringContent(
        //        JsonSerializer.Serialize(requestContent),
        //        Encoding.UTF8,
        //        "application/json"
        //    );

        //    var response = await _httpClient.SendAsync(request);

        //    var responseBody = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        _logger.LogError(
        //            "OpenAI TTS failed. Status: {StatusCode}, Response: {Response}",
        //            response.StatusCode,
        //            responseBody
        //        );

        //        return null;
        //    }

        //    return new MemoryStream(
        //        Encoding.UTF8.GetBytes(responseBody)
        //    );
        //}
        private async Task<string> GetAIResponse(string question)
        {
            try
            {
                // Escape newlines to fix JSON errors
                question = question.Replace("\n", "\\n").Replace("\r", "\\r");
                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "system", content = "Act as an interview coach. Help me answer this question in a professional and impactful way." },
                        new { role = "user", content = question }
                    },
                    max_tokens = 500,
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                _logger.LogInformation("Sending request to OpenAI: {0}", JsonSerializer.Serialize(requestBody, options));

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                string responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Received response from OpenAI: {0}", responseString);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API Error: {0}", responseString);
                    return null;
                }

                var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

                if (responseObject?.Choices != null && responseObject.Choices.Count > 0)
                {
                    return responseObject.Choices[0].Message.Content;
                }

                return "No response from AI.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return null;
            }
        }

        private async Task<Stream?> GenerateSpeechFromText(string text)
        {
            var voice = configuration["OpenAI:TTSVoice"] ?? "shimmer";
            var requestContent = new
            {
                model = "tts-1",
                input = text,
                voice = voice // alloy, echo, fable, onyx, nova, shimmer
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("TTS generation failed: {0}", await response.Content.ReadAsStringAsync());
                return null;
            }

            return await response.Content.ReadAsStreamAsync();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("stream-speech-to-text")]
        public async Task<IActionResult> StreamSpeechToText([FromForm] IFormFile audioChunk)
        {
            if (audioChunk == null || audioChunk.Length == 0)
            {
                return BadRequest(new { message = "No audio chunk provided." });
            }

            try
            {
                using var stream = new MemoryStream();
                await audioChunk.CopyToAsync(stream);
                stream.Position = 0;

                var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(audioChunk.ContentType ?? "audio/wav");
                content.Add(fileContent, "file", audioChunk.FileName);
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent("en"), "language");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
                var responseString = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Whisper chunk STT failed: {0}", responseString);
                    return StatusCode((int)response.StatusCode, new { message = "Chunk transcription failed." });
                }
                var whisperResponse = JsonSerializer.Deserialize<WhisperResponse>(responseString);
                return Ok(new { partialText = whisperResponse?.Text });
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chunked STT");
                return StatusCode(500, new { message = "Internal Server Error" });
            }
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("speech-to-text")]
        public async Task<IActionResult> ConvertSpeechToText([FromForm] IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest(new { message = "No audio file provided." });
            }
            try
            {
                _logger.LogInformation("Received audio file: {0}", audioFile.FileName);

                using var stream = new MemoryStream();
                await audioFile.CopyToAsync(stream);
                stream.Position = 0;


                var content = new MultipartFormDataContent();
                var responseContent = new MediaTypeWithQualityHeaderValue(audioFile.ContentType);
                var fileContent = new StreamContent(stream);
                var cleanMimeType = audioFile.ContentType?.Split(';')[0];
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(cleanMimeType ?? "audio/webm");
                content.Add(fileContent, "file", audioFile.FileName);
                // Specify model and language options 
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent("en"), "language");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
                string responseString = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Whisper API Response: {0}", responseString);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error from OpenAI Whisper API: {0}", responseString);
                    return StatusCode((int)response.StatusCode, new { message = "Speech-to-text conversion failed." });
                }
                var responseObject = JsonSerializer.Deserialize<WhisperResponse>(responseString);
                return Ok(new { text = responseObject?.Text ?? "No transcription available." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing speech-to-text request");
                return StatusCode(500, new { message = "Internal Server Error" });
            }
        }
        [HttpPost("merge-and-process")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> MergeAndProcess([FromBody] MergeRequest request)
        {
            if (string.IsNullOrEmpty(request.SessionId))
                return BadRequest(new { message = "Session ID is required." });
            var tempRoot = Path.Combine(_env.ContentRootPath, "TempUploads", request.SessionId);
            if (!Directory.Exists(tempRoot))
                return BadRequest(new { message = "No chunks found for this session." });

            var mergedFilePath = Path.Combine(_env.ContentRootPath, "TempUploads", $"{request.SessionId}_merged.mp3");

            try
            {
                //  Step 1: Merge chunks safely
                var chunkFiles = Directory.GetFiles(tempRoot)
                    .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f).Split('_')[1]))
                    .ToList();
                await using (var output = new FileStream(mergedFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (var chunk in chunkFiles)
                    {
                        bool readSuccess = false;
                        for (int retry = 0; retry < 5 && !readSuccess; retry++)
                        {
                            try
                            {
                                using (var input = new FileStream(chunk, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    await input.CopyToAsync(output);
                                }
                                readSuccess = true;
                            }
                            catch (IOException)
                            {
                                _logger.LogWarning($"Chunk {chunk} locked, retrying {retry + 1}/5...");
                                await Task.Delay(300);
                            }
                        }

                        if (!readSuccess)
                            throw new IOException($"Failed to read chunk {chunk} after retries.");
                    }
                }

                _logger.LogInformation(" Merging completed successfully for session {0}", request.SessionId);

                //  Step 2: Wait until file handle is released 
                for (int retry = 0; retry < 10; retry++)
                {
                    try
                    {
                        using (FileStream fs = System.IO.File.Open(mergedFilePath, FileMode.Open, FileAccess.Read, FileShare.None))

                        {
                            // If we reach here, file is free
                            break;
                        }
                    }
                    catch (IOException)
                    {
                        _logger.LogWarning($"Merged file still locked, waiting... ({retry + 1}/10)");
                        await Task.Delay(500);
                    }
                }

                //  Step 3: Delete chunks safely
                foreach (var chunk in chunkFiles)
                {
                    try
                    {
                        System.IO.File.Delete(chunk);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to delete chunk {chunk}: {ex.Message}");
                    }
                }

                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not delete temp folder: {ex.Message}");
                }

                //  Step 4: Process audio AFTER full release
                IActionResult result;
                await Task.Delay(1000); // allow IO to settle fully

                await using (var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var formFile = new FormFile(fileStream, 0, fileStream.Length, "audioFile", Path.GetFileName(mergedFilePath))
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = "audio/mp3"
                    };

                    result = await ConvertSpeechToTextMeeting(formFile);
                }

                //  Step 5: Delete merged file safely (after conversion done)
                for (int retry = 0; retry < 5; retry++)
                {
                    try
                    {
                        if (System.IO.File.Exists(mergedFilePath))
                            System.IO.File.Delete(mergedFilePath);

                        _logger.LogInformation(" Deleted merged file: {0}", mergedFilePath);
                        break;
                    }
                    catch (IOException)
                    {
                        _logger.LogWarning($"File still locked, retrying delete... ({retry + 1}/5)");
                        await Task.Delay(500);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error during merge or summary process");
                return StatusCode(500, new { message = "Internal Server Error", details = ex.Message });
            }
        }


        public class MergeRequest
        {
            public string SessionId { get; set; }
        }
        
        [HttpPost("upload-chunk")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadChunk(IFormFile chunk, [FromForm] int index, [FromForm] int total, [FromForm] string sessionId)
        {
            if (chunk == null || chunk.Length == 0)
                return BadRequest(new { message = "Invalid chunk" });

            var uploadPath = Path.Combine(_env.ContentRootPath, "TempUploads", sessionId);
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, $"chunk_{index}.part");
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await chunk.CopyToAsync(stream);
            }

            return Ok(new { message = $"Chunk {index + 1}/{total} uploaded successfully." });
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("speech-to-text-meeting")]
        [RequestSizeLimit(1_000_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 1_000_000_000)]
        public async Task<IActionResult> ConvertSpeechToTextMeeting([FromForm] IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
                return BadRequest(new { message = "No audio file provided." });

            string tempPath = string.Empty;
            var chunks = new List<string>();

            try
            {
                // 1️⃣ Save uploaded file temporarily
                tempPath = Path.Combine(_env.ContentRootPath, "TempUploads", Guid.NewGuid() + ".mp3");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

                await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await audioFile.CopyToAsync(fileStream);
                }

                _logger.LogInformation("File saved temporarily at {0}", tempPath);

                // 2️⃣ Split large file into smaller chunks (e.g., 24 MB)
                const int chunkSizeMB = 24;
                chunks = await SplitAudioFileAsync(tempPath, chunkSizeMB);

                var fullTranscript = new StringBuilder();

                // 3️⃣ Transcribe each chunk
                foreach (var chunk in chunks)
                {
                    _logger.LogInformation("Processing chunk: {0}", chunk);

                    // Ensure TranscribeChunkAsync disposes any stream internally
                    var transcript = await TranscribeChunkAsync(chunk);
                    if (!string.IsNullOrEmpty(transcript))
                        fullTranscript.AppendLine(transcript);
                }

                // 4️⃣ Delete chunks safely with retry logic
                foreach (var chunk in chunks)
                {
                    bool deleted = false;
                    int retries = 5;

                    while (!deleted && retries > 0)
                    {
                        try
                        {
                            System.IO.File.Delete(chunk);
                            deleted = true;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(200);
                            retries--;
                        }
                    }

                    if (!deleted)
                        _logger.LogWarning("Failed to delete chunk file after retries: {0}", chunk);
                }

                // 5️⃣ Delete main temp file safely
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                        await Task.Delay(500);
                        if (System.IO.File.Exists(tempPath))
                            System.IO.File.Delete(tempPath);
                    }
                }

                // 6️⃣ Summarize the transcription
                var summary = await SummarizeTextWithGPT(fullTranscript.ToString());

                // 7️⃣ Return result
                return Ok(new
                {
                    transcript = fullTranscript.ToString(),
                    summary = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during speech-to-text-meeting process");
                return StatusCode(500, new { message = "Internal Server Error", details = ex.Message });
            }
            finally
            {
                // 8️⃣ Extra safety cleanup
                foreach (var chunk in chunks)
                {
                    if (System.IO.File.Exists(chunk))
                    {
                        try { System.IO.File.Delete(chunk); } catch { /* ignored */ }
                    }
                }

                if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath))
                {
                    try { System.IO.File.Delete(tempPath); } catch { /* ignored */ }
                }
            }
        }

        private async Task<List<string>> SplitAudioFileAsync(string filePath, int chunkSizeMB)
        {
            var chunkPaths = new List<string>();
            var chunkSizeBytes = chunkSizeMB * 1024 * 1024;
            byte[] buffer = new byte[chunkSizeBytes];
            int bytesRead, index = 0;

            // Wait until file is ready to read (retry up to 10 times)
            for (int retry = 0; retry < 10; retry++)
            {
                try
                {
                    using (var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // ✅ Safe chunk splitting with full close between writes
                        while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            var chunkPath = Path.Combine(Path.GetDirectoryName(filePath)!, $"chunk_{index++}.mp3");

                            // Write each chunk separately and ensure flush + close before next
                            using (var output = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                                await output.FlushAsync();
                            }

                            chunkPaths.Add(chunkPath);
                        }
                    }

                    break; // file read successfully, exit retry loop
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"⚠️ File still locked, retrying... ({retry + 1}/10): {ex.Message}");
                    await Task.Delay(500);
                }
            }

            // Wait briefly to ensure OS releases file handles
            await Task.Delay(300);

            return chunkPaths;
        }

        private async Task<string> TranscribeChunkAsync(string chunkPath)
        {
            int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var content = new MultipartFormDataContent();
                    await using var fileStream = System.IO.File.OpenRead(chunkPath);
                    using var fileContent = new StreamContent(fileStream);

                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mp3");
                    content.Add(fileContent, "file", Path.GetFileName(chunkPath));
                    content.Add(new StringContent("whisper-1"), "model");

                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                    var response = await _httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);

                    string responseString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Whisper API failed (attempt {0}) for {1}: {2}", attempt, chunkPath, responseString);
                        await Task.Delay(1000 * attempt);
                        continue; // retry
                    }

                    var responseObject = JsonSerializer.Deserialize<WhisperResponse>(responseString);
                    return responseObject?.Text ?? string.Empty;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("Network/SSL error on chunk {0} (attempt {1}): {2}", chunkPath, attempt, ex.Message);
                    await Task.Delay(1500 * attempt);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Unexpected error on chunk {0}: {1}", chunkPath, ex.Message);
                    break; // skip if unrecoverable
                }
            }

            return string.Empty; // fallback if all retries fail
        }




        // Helper function to summarize text using OpenAI GPT
        private async Task<string> SummarizeTextWithGPT(string text)
        {
            var requestBody = new
            {
                model = "gpt-4o",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = @"You are a highly skilled professional summarizer. Your task is to read detailed, lengthy texts and generate a comprehensive and detailed summary. The summary must cover everything that has been discussed in the text and should NOT be short or overly condensed.\n\nThe summary must include:\n1. A clear introduction (2–3 lines) explaining the context and purpose of the content.\n2. A detailed list of key points, explaining each important idea properly (use bullet points where appropriate).\n3. All important facts, figures, examples, or statistics mentioned in the text (if available).\n4. A well-explained conclusion that clearly reflects the overall discussion and outcomes.\n\nThe tone should be formal, professional, clear, and easy to understand. Do not skip any important discussion points. Avoid unnecessary repetition, but ensure no meaningful information is lost."
                    },
                    new
                    {
                        role = "user",
                        content = $"Create a detailed and comprehensive summary of the following text. Make sure the summary fully explains everything that has been discussed, rather than producing a short or high-level overview:\n\n{text}\n\nEnsure the final output strictly follows the structured format mentioned above and provides a complete representation of the discussion."

                    }
                },
                max_tokens = 250
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            _logger.LogInformation("Sending request to OpenAI: {0}", JsonSerializer.Serialize(requestBody, options));

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            string responseString = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Received response from OpenAI: {0}", responseString);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI API Error: {0}", responseString);
                return null;
            }

            var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

            if (responseObject?.Choices != null && responseObject.Choices.Count > 0)
            {
                return responseObject.Choices[0].Message.Content;
            }

            return "No response from AI.";
        }




        [HttpPost("generate-summary")]
        public async Task<IActionResult> GenerateAndSendSummary([FromBody] InterviewSummaryRequest model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.ChatText) || string.IsNullOrWhiteSpace(model.Email))
                    return BadRequest("Missing chat or email");
                var user = await _userManager.FindByEmailAsync(model.Email);
                var userPersona = _dbContext.Personas.Where(p => user.Id == p.UserId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();
                var latestLiveReport = await _dbContext.InterviewReports
                .Where(r => r.UserId == user.Id && r.InterviewType == "Live")
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

                string interviewDate = latestLiveReport?.CreatedAt.ToString("dd MMMM, yyyy") ?? "N/A";
                string interviewDuration = latestLiveReport?.Duration ?? "N/A";

                //summarize the interview chat
                var summary = await SummarizeTextWithGPT(model.ChatText);

                if (string.IsNullOrWhiteSpace(summary))
                    return StatusCode(500, "Failed to generate summary");
                var summaryHtml = ConvertPlainTextToHtml(summary);
                // Send summary 
                await EmailHelper.SendSummaryEmail(model.Email, model.DisplayName, summaryHtml, userPersona.CompanyName, interviewDate, interviewDuration);

                return Ok(new { message = "Summary generated and email sent." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Summary generation failed.");
                return StatusCode(500, "An error occurred while generating summary.");
            }
        }

        private static string ConvertPlainTextToHtml(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return "<p>No summary available.</p>";


            var safeText = System.Net.WebUtility.HtmlEncode(plainText);

            // Convert line breaks to HTML
            safeText = safeText
                .Replace("\r\n", "\n")        // Normalize line endings
                .Replace("\n\n", "</p><p>")   // Paragraphs
                .Replace("\n", "<br>");       // Line breaks

            return $"<p>{safeText}</p>";
        }


        public class InterviewSummaryRequest
        {
            public string ChatText { get; set; }
            public string Email { get; set; }
            public string DisplayName { get; set; }
        }

        [HttpPost("create-persona")]
        public async Task<IActionResult> CreatePersona([FromForm] PersonaDTO dto)
        {
            try
            {
                string resumePath = null;

                if (dto.ResumeFile != null && dto.ResumeFile.Length > 0)
                {
                    // Get content root path (production safe)
                    var contentRoot = _env.ContentRootPath;
                    var uploadsFolder = Path.Combine(contentRoot, "wwwroot", "uploads");

                    Console.WriteLine($"ContentRootPath: {contentRoot}");
                    Console.WriteLine($"Uploads folder full path: {uploadsFolder}");

                    try
                    {
                        // Create directory if it doesn't exist
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                            Console.WriteLine("Uploads directory created successfully.");
                        }
                    }
                    catch (Exception dirEx)
                    {
                        Console.WriteLine($"Failed to create uploads directory: {dirEx.Message}");
                        return StatusCode(500, "Server cannot create uploads folder. Check folder permissions.");
                    }

                    try
                    {
                        var fileName = Path.GetFileName(dto.ResumeFile.FileName);
                        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        Console.WriteLine($"Saving file to: {filePath}");

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await dto.ResumeFile.CopyToAsync(stream);
                        }

                        resumePath = $"/uploads/{uniqueFileName}";
                        Console.WriteLine($"File saved successfully. ResumePath: {resumePath}");
                    }
                    catch (Exception fileEx)
                    {
                        Console.WriteLine($"File saving failed: {fileEx.Message}");
                        return StatusCode(500, "Error while saving resume file. Check file path and write permissions.");
                    }
                }
                else
                {
                    Console.WriteLine("Resume file missing or empty.");
                    return BadRequest("Resume file is missing or empty.");
                }

                var persona = new Persona
                {
                    CompanyName = dto.CompanyName,
                    CompanyDescription = dto.CompanyDescription,
                    JobTitle = dto.JobTitle,
                    JobDescription = dto.JobDescription,
                    ResumePath = resumePath,
                    UserId = dto.UserId,
                    InterviewDomain = dto.InterviewDomain,
                    InterviewType = dto.InterviewType,
                    CreatedAt = DateTime.UtcNow
                };

                Console.WriteLine("Adding persona to DB...");

                _dbContext.Personas.Add(persona);
                await _dbContext.SaveChangesAsync();

                Console.WriteLine("Persona created successfully!");

                return Ok(new { message = "Persona created successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error while creating persona: {ex.Message}");
                return StatusCode(500, "Internal server error occurred while processing your request.");
            }
        }
        // DTOs
        public class CareerGenerateRequest
        {
            public string Tool { get; set; } = "cover"; // cover | thanks | follow | recruit
            public string Tone { get; set; } = "concise"; // concise | warm | formal
            public CommonDto Common { get; set; }
            public Dictionary<string, string> Specific { get; set; } = new();
        }

        public class CommonDto
        {
            public string Name { get; set; }
            public string Email { get; set; }
            public string Role { get; set; }
            public string Company { get; set; }
            public string Contact { get; set; }
        }

        public class CareerGenerateResponse
        {
            public string Output { get; set; }
        }

        [HttpGet("check-limit/{userId}")]
        public async Task<IActionResult> CheckResumeLimit(string userId)
        {
            // Get subscription
            var subscription = await _dbContext.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (subscription == null)
            {
                return NotFound(new { message = "Subscription not found" });
            }
            var planName = subscription.PlanName ?? "Free";
            // Get resume count
            var resumeCount = await _dbContext.Resumes
                .CountAsync(r => r.UserId == userId);

            bool canCreateMore = true;

            if (planName.Equals("Free", StringComparison.OrdinalIgnoreCase))
            {
                canCreateMore = resumeCount < 1;
            }
            else
            {
                canCreateMore = true;
            }

            return Ok(new
            {
                planName,
                resumeCount,
                canCreateMore
            });
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] CareerGenerateRequest req)
        {
            if (req == null) return BadRequest();

            try
            {
                // build prompt depending on tool
                string prompt = BuildPrompt(req);

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 400
                };
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI error: {0}", responseString);
                    // return fallback output same as frontend local template
                    return Ok(new CareerGenerateResponse { Output = BuildLocalFallback(req) });
                }

                using var doc = JsonDocument.Parse(responseString);
                var choiceContent = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                // return trimmed text
                return Ok(new CareerGenerateResponse { Output = choiceContent.Trim() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generate failed");
                return Ok(new CareerGenerateResponse { Output = BuildLocalFallback(req) });
            }
        }



        private string BuildPrompt(CareerGenerateRequest req)
        {
            var c = req.Common ?? new CommonDto();
            var tone = req.Tone ?? "concise";
            string greet = tone == "formal" ? "Dear" : "Hi";
            string close = tone == "formal" ? "Sincerely" : tone == "warm" ? "Best regards" : "Best";

            if (req.Tool == "cover")
            {
                var jd = req.Specific.GetValueOrDefault("jd") ?? "";
                var highlights = req.Specific.GetValueOrDefault("highlights") ?? "";
                return $@"
                You are a professional cover-letter writer. Produce a concise {tone} cover letter in plain text.
                Use the following inputs and format the output exactly (no metadata): 
                - Greeting single line using '{greet}' 
                - Body that references role: '{c.Role}', company: '{c.Company}', contact: '{c.Contact}'
                - Mention 3 concise bullet points drawn from highlights (each starting with '•')
                - One closing sentence offering to discuss projects and a signoff using '{close}'
                Inputs:
                Name: {c.Name}
                Email: {c.Email}
                Job Description: {jd}
                Highlights (one per line): {highlights}
                ";
            }

            if (req.Tool == "thanks")
            {
                var to = req.Specific.GetValueOrDefault("to") ?? "Team";
                var focus = req.Specific.GetValueOrDefault("focus") ?? "our conversation";
                var moment = req.Specific.GetValueOrDefault("moment") ?? "";
                return $@"
                You are a professional writer. Create a {tone} thank you email.
                Keep it short and personal. Use greeting '{greet} {to}' and sign with '{close}, {c.Name}'.
                Reference: {focus} {(string.IsNullOrWhiteSpace(moment) ? "" : "especially " + moment)}.
                Do not include a subject line.
                ";
            }

            if (req.Tool == "follow")
            {
                var to = req.Specific.GetValueOrDefault("to") ?? "there";
                var when = req.Specific.GetValueOrDefault("when") ?? "our recent conversation";
                var ask = req.Specific.GetValueOrDefault("ask") ?? "next steps";
                return $@"
                Write a {tone} follow-up email. Start with '{greet} {to},' mention '{when}', and ask about '{ask}'.
                Keep it short and actionable and end with '{close}, {c.Name}'.
                Do not include a subject line.
                ";
            }

            // recruit
            {
                var to = req.Specific.GetValueOrDefault("to") ?? "there";
                var targetRole = req.Specific.GetValueOrDefault("targetRole") ?? c.Role ?? "Software Engineer";
                var fit = req.Specific.GetValueOrDefault("fit") ?? "";
                return $@"
                You are a recruiter outreach copywriter. Write a {tone} outreach message for {targetRole} at {c.Company}.
                Use 2-3 concise bullets for fit drawn from this input: {fit}.
                Keep greeting '{greet} {to}', finish with '{close}, {c.Name}'.
                Do not include a subject line.
                ";
            }

        }

        // fallback server-side local template (similar to frontend)
        private string BuildLocalFallback(CareerGenerateRequest req)
        {
            var c = req.Common ?? new CommonDto();
            var tone = req.Tone ?? "concise";
            string greet = tone == "formal" ? "Dear" : "Hi";
            string close = tone == "formal" ? "Sincerely" : tone == "warm" ? "Best regards" : "Best";

            if (req.Tool == "cover")
            {
                var highlights = req.Specific.GetValueOrDefault("highlights") ?? "";
                var bullets = string.IsNullOrWhiteSpace(highlights) ? "• Relevant experience aligning with the JD" :
                    string.Join("\n", highlights.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(h => "• " + h.Trim()));
                var projectName = "a recent project";
                var sum = "I build reliable software and measure impact.";
                return $"{greet} {req.Common?.Contact ?? "Hiring Team"},\n\nI'm interested in the {(c.Role ?? "role")} role at {(c.Company ?? "your company")}. What this really means is I can help you ship faster without breaking things.\n\nHere’s the thing: after reviewing the job description, a few matches stood out:\n\n{bullets}\n\nA bit more context: {sum}\n\nIf it helps, I can walk through {projectName} and the decisions behind it.\n\n{close},\n{c.Name}\n{c.Email}";
            }

            if (req.Tool == "thanks")
            {
                var to = req.Specific.GetValueOrDefault("to") ?? "Team";
                var focus = req.Specific.GetValueOrDefault("focus") ?? "our conversation";
                var moment = req.Specific.GetValueOrDefault("moment") ?? "";
                return $"{greet} {to},\n\nThanks for the time today. I enjoyed {focus}{(string.IsNullOrWhiteSpace(moment) ? "" : $"—especially {moment}")}. The role lines up with where I do my best work.\n\nIf anything else would help—references, code samples, deeper dives—happy to send.\n\n{close},\n{c.Name}\n{c.Email}";
            }

            if (req.Tool == "follow")
            {
                var to = req.Specific.GetValueOrDefault("to") ?? "there";
                var when = req.Specific.GetValueOrDefault("when") ?? "our recent conversation";
                var ask = req.Specific.GetValueOrDefault("ask") ?? "next steps";
                return $"{greet} {to},\n\nQuick follow-up on {when}. Any updates on {ask}?\n\nHappy to provide anything else you need.\n\n{close},\n{c.Name}\n{c.Email}";
            }

            // recruit
            {
                var to = req.Specific.GetValueOrDefault("to") ?? "there";
                var targetRole = req.Specific.GetValueOrDefault("targetRole") ?? (c.Role ?? "Software Engineer");
                var fit = req.Specific.GetValueOrDefault("fit") ?? "";
                var bullets = string.IsNullOrWhiteSpace(fit) ? "• Experience shipping production systems\n• Strong communication and ownership" :
                    string.Join("\n", fit.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(f => "• " + f.Trim()));
                return $"{greet} {to},\n\nI’m exploring {targetRole} opportunities at {c.Company ?? "your company"}. A few quick reasons I might be a fit:\n\n{bullets}\n\nOpen to a quick chat if helpful.\n\n{close},\n{c.Name}\n{c.Email}";
            }
        }




        public class EnhanceRequest
        {
            public string Text { get; set; }

            public string Type { get; set; } // summary | job | project | skills
        }

        public class EnhanceResponse
        {
            public string Polished { get; set; }
        }

        [HttpPost("enhance-text")]
        public async Task<IActionResult> EnhanceText([FromBody] EnhanceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { error = "Text is required" });

            try
            {
                string prompt;

                if (request.Type?.ToLower() == "skills")
                {

                    prompt = $@"
                    You are a professional resume writer.
                    Polish and refine the following list of skills. 
                    - Keep only skills (no descriptions, no sentences).
                    - Correct spelling, standardize names (e.g., 'js' → 'JavaScript').
                    - Remove duplicates.
                    - Return ONLY a clean, comma-separated list of skills.

                    Skills: {request.Text}";
                }
                else
                {

                    //prompt = $@"
                    //You are a professional resume writer.
                    //Your task is to refine and polish resume content to make it more impactful, 
                    //professional, and concise while preserving the original meaning.

                    //IMPORTANT:
                    //- Keep the same format and structure as the input.
                    //- Do not add bullet points, numbering, symbols (*, -, etc.), or extra headings.
                    //- Return the text only, in the same form it was provided.

                    //Polish this {request.Type} for a resume, keeping the original format:
                    //{request.Text}";
                    prompt = $@"
                    You are a professional resume writer.
                    Your task is to refine, polish, and slightly expand resume content 
                    to make it more impactful, professional, and concise while preserving the original meaning.

                    IMPORTANT:
                    - Keep the same format and structure as the input.
                    - Do not add bullet points, numbering, symbols (*, -, etc.), or extra headings.
                    - Return the text only, in the same form it was provided.
                    - Slightly expand the content with relevant professional details, but do not make it unnecessarily long.

                    Polish and expand this {request.Type} for a resume, keeping the original format:
                    {request.Text}";


                }

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            },
                    max_tokens = 400
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API Error: {0}", responseString);
                    return StatusCode(500, new { error = "OpenAI API request failed" });
                }

                var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

                if (responseObject?.Choices != null && responseObject.Choices.Count > 0)
                {
                    return Ok(new EnhanceResponse
                    {
                        Polished = responseObject.Choices[0].Message.Content.Trim()
                    });
                }

                return Ok(new EnhanceResponse { Polished = "No response from AI." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EnhanceText failed");
                return StatusCode(500, new { error = "Unexpected server error", details = ex.Message });
            }
        }

        [HttpPost("enhance-skills")]
        public async Task<IActionResult> EnhanceSkills([FromBody] SkillEnhanceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.JobTitle) || request.Skills == null || request.Skills.Count == 0)
                return BadRequest(new { error = "JobTitle and Skills are required" });

            try
            {
                string prompt = $@"
                You are a professional resume coach.
                Based on the job description '{request.JobTitle}', polish and enhance the following skills:
                - Expand the list with other relevant skills for this job.
                - Correct spelling and standardize names (e.g., 'js' → 'JavaScript').
                - Remove duplicates.
                - Return ONLY a clean, comma-separated list of skills.
                - Ensure each skill starts with a capital letter.

                Skills: {string.Join(", ", request.Skills)}";

                var requestBody = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            },
                    max_tokens = 300
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _openAiApiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI API Error: {0}", responseString);
                    return StatusCode(500, new { error = "OpenAI API request failed" });
                }

                var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

                if (responseObject?.Choices != null && responseObject.Choices.Count > 0)
                {
                    string polished = responseObject.Choices[0].Message.Content.Trim();


                    var skills = polished
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.Trim().ToLower()))
                        .ToList();

                    return Ok(new EnhanceResponse
                    {
                        Polished = string.Join(", ", skills)
                    });
                }

                return Ok(new EnhanceResponse { Polished = "No response from AI." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EnhanceSkills failed");
                return StatusCode(500, new { error = "Unexpected server error", details = ex.Message });
            }
        }

        public class SkillEnhanceRequest
        {
            public string JobTitle { get; set; }
            public List<string> Skills { get; set; }
        }

        //[HttpPost("create-persona")]
        //public async Task<IActionResult> CreatePersona([FromForm] PersonaDTO dto)
        //{
        //    //return Ok(new { message = "Persona created successfully!" });

        //    try
        //    {
        //        string resumePath = null;

        //        if (dto.ResumeFile != null && dto.ResumeFile.Length > 0)
        //        {
        //            var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");

        //            if (!Directory.Exists(uploadsFolder))
        //            {
        //                Directory.CreateDirectory(uploadsFolder);
        //            }

        //            var fileName = Path.GetFileName(dto.ResumeFile.FileName);
        //            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        //            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        //            using (var stream = new FileStream(filePath, FileMode.Create))
        //            {
        //                await dto.ResumeFile.CopyToAsync(stream);
        //            }

        //            resumePath = $"/uploads/{uniqueFileName}";

        //        }
        //        else
        //        {
        //            return BadRequest("Resume file is missing or empty.");
        //        }

        //        var persona = new Persona
        //        {
        //            CompanyName = dto.CompanyName,
        //            CompanyDescription = dto.CompanyDescription,
        //            JobTitle = dto.JobTitle,
        //            JobDescription = dto.JobDescription,
        //            ResumePath = resumePath,
        //            UserId = dto.UserId,
        //            InterviewDomain = dto.InterviewDomain,
        //            InterviewType = dto.InterviewType,
        //            CreatedAt = DateTime.UtcNow
        //        };

        //        _dbContext.Personas.Add(persona);
        //        await _dbContext.SaveChangesAsync();

        //        return Ok(new { message = "Persona created successfully!" });
        //    }
        //    catch (Exception ex)
        //    {

        //        Console.WriteLine($"Error while creating persona: {ex.Message}");

        //        return StatusCode(500, "Internal server error occurred while processing your request.");
        //    }
        //}

        //[HttpPost("create")]
        //public async Task<IActionResult> CreatePersona([FromBody] PersonaDTO dto)
        //{
        //    string resumePath = null;

        //    if (!string.IsNullOrEmpty(dto.ResumeBase64))
        //    {
        //        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
        //        if (!Directory.Exists(uploadsFolder))
        //        {
        //            Directory.CreateDirectory(uploadsFolder);
        //        }

        //        // Extract base64 data
        //        var base64Data = Regex.Match(dto.ResumeBase64, @"data:(.+?);base64,(.+)").Groups[2].Value;
        //        var fileBytes = Convert.FromBase64String(base64Data);
        //        var uniqueFileName = $"{Guid.NewGuid()}.pdf"; 

        //        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        //        await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

        //        resumePath = $"/uploads/{uniqueFileName}";
        //    }

        //    var persona = new Persona
        //    {
        //        CompanyName = dto.CompanyName,
        //        CompanyDescription = dto.CompanyDescription,
        //        JobTitle = dto.JobTitle,
        //        JobDescription = dto.JobDescription,
        //        ResumePath = resumePath,
        //        UserId = dto.UserId,
        //        InterviewDomain = dto.InterviewDomain,
        //        InterviewType = dto.InterviewType,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    _dbContext.Personas.Add(persona);
        //    await _dbContext.SaveChangesAsync();

        //    return Ok(new { message = "Persona created successfully!" });
        //}

        [HttpPut("update-domain")]
        public IActionResult UpdateInterviewDomain([FromBody] InterviewDomainUpdateDto dto)
        {
            var persona = _dbContext.Personas
                             .Where(u => u.UserId == dto.Email)
                             .OrderByDescending(u => u.CreatedAt)
                             .FirstOrDefault();
            if (persona == null) return NotFound("User not found");

            persona.InterviewDomain = dto.InterviewDomain;
            _dbContext.Entry(persona).Property(p => p.InterviewDomain).IsModified = true;
            _dbContext.SaveChangesAsync();

            return Ok(new { message = "Interview domain updated." });
        }

        //private async Task<Persona> GetUserPersonaByEmailAsync(string email)
        //{
        //    var persona = await _dbContext.Personas.FirstOrDefaultAsync(p => p.UserId == email);
        //    return persona;
        //}


        private async Task<Persona> GetUserPersonaByEmailAsync(string email)
        {
            var persona = await _dbContext.Personas
                .Where(p => p.UserId == email)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            return persona;
        }
        public class InterviewDomainUpdateDto
        {
            public string Email { get; set; }
            public string InterviewDomain { get; set; }
        }

    }

    public class AIQuestionModel
    {
        public string Question { get; set; }
        public string Email { set; get; }
    }

    public class SimpleAIQuestionModel
    {
        public string Question { get; set; }
    }

    public class OpenAIResponse
    {
        public List<Choice> Choices { get; set; }
    }
    public class NextQuestionsDTO
    {
        public string UserEmail { get; set; }
    }

    public class Choice
    {
        public Message Message { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    }
    public class WhisperResponse
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
