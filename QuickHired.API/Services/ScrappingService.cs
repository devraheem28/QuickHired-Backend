using HtmlAgilityPack;
using QuickHired.API.Controllers;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace QuickHired.API.Services
{
    public class ScrapingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ScrapingService> _logger;
        private readonly string _openAiApiKey;
        public ScrapingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _openAiApiKey = configuration["OpenAI:ApiKey"];
        }
        public async Task<List<string>> GetInterviewQuestions(string company, string role)
        {
            var questions = new List<string>();
            var apiKey = "a02afea91a1573ac59243edc13154be5";


            try
            {
                var query = $"{company} {role}".Trim();
                var querySlug = query.Replace(" ", "-").ToLower();
                var searchUrl = $"https://www.glassdoor.com/Interview/{querySlug}-interview-questions-SRCH_KO0,{query.Length}.htm";
                var scraperSearchUrl = $"https://api.scraperapi.com?api_key={apiKey}&url={Uri.EscapeDataString(searchUrl)}";

                var searchResponse = await _httpClient.GetStringAsync(scraperSearchUrl);

                var searchDoc = new HtmlDocument();
                searchDoc.LoadHtml(searchResponse);

                var nodes = searchDoc.DocumentNode.SelectNodes("//div[contains(@class, 'InterviewQuestion_interviewQuestionContainer__dU_tD')]");

                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        var lines = HtmlEntity.DeEntitize(node.InnerText)
                            .Replace("\r", "")
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

                        var combinedText = string.Join(" ", lines);

                        var questionCandidates = Regex.Matches(combinedText, @"(Why|What|How|Tell me).*[\?]", RegexOptions.IgnoreCase);

                        foreach (Match q in questionCandidates)
                        {
                            var question = q.Value.Trim();

                            if (!string.IsNullOrWhiteSpace(question) && question.Length > 10 && !question.Contains("★"))
                            {
                                questions.Add(question);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                await File.WriteAllTextAsync("glassdoor-error.log", ex.ToString());
            }


            try
            {
                var aiQuestions = await GetAIInterviewQuestions(company, role);

                if (aiQuestions != null && aiQuestions.Any())
                {
                    questions.AddRange(aiQuestions);
                }
            }
            catch (Exception ex)
            {

                await File.WriteAllTextAsync("ai-error.log", ex.ToString());
            }

            return questions;
        }

        //public async Task<List<string>> GetInterviewQuestions(string company, string role)
        //{
        //    var questions = new List<string>();
        //    var apiKey = "6005b8248b01d89004c8bb67097ba93e";
        //    try
        //    {
        //        var query = $"{company} {role}".Trim();
        //        var querySlug = query.Replace(" ", "-").ToLower();
        //        var searchUrl = $"https://www.glassdoor.com/Interview/{querySlug}-interview-questions-SRCH_KO0,{query.Length}.htm";
        //        var scraperSearchUrl = $"http://api.scraperapi.com?api_key={apiKey}&url={Uri.EscapeDataString(searchUrl)}";

        //        var searchResponse = await _httpClient.GetStringAsync(scraperSearchUrl);

        //        // Save response for debugging
        //        await File.WriteAllTextAsync("glassdoor-response.html", searchResponse);
        //        var searchDoc = new HtmlDocument();
        //        searchDoc.LoadHtml(searchResponse);

        //        var nodes = searchDoc.DocumentNode.SelectNodes("//div[contains(@class, 'InterviewQuestion_interviewQuestionContainer__dU_tD')]");

        //        if (nodes != null)
        //        {
        //            foreach (var node in nodes)
        //            {
        //                var lines = HtmlEntity.DeEntitize(node.InnerText)
        //                                      .Replace("\r", "")
        //                                      .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        //                                      .Select(l => l.Trim())
        //                                      .Where(l => !string.IsNullOrWhiteSpace(l))
        //                                      .ToList();

        //                // Combine lines to have everything in one string
        //                var combinedText = string.Join(" ", lines);
        //                var questionCandidates = Regex.Matches(combinedText, @"(Why|What|How|Tell me).*[\?]", RegexOptions.IgnoreCase);

        //                // Adding valid questions to list
        //                foreach (Match q in questionCandidates)
        //                {
        //                    var question = q.Value.Trim();


        //                    if (!string.IsNullOrWhiteSpace(question) && question.Length > 10 && !question.Contains("★"))
        //                    {
        //                        questions.Add(question);
        //                    }
        //                }
        //            }

        //        }


        //    }
        //    catch (Exception ex)
        //    {
        //        await File.WriteAllTextAsync("glassdoor-error.log", ex.ToString());
        //    }

        //    return questions;
        //}
        private async Task<List<string>> GetAIInterviewQuestions(string company, string role)
        {
            try
            {
                var prompt = $"Generate a list of 50 to 52 common interview questions for the position of '{role}' at a company like '{company}'. Return the questions only.";

                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                new { role = "system", content = "You are an expert career coach." },
                new { role = "user", content = prompt }
            },
                    max_tokens = 500,
                    temperature = 0.7
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
                    return new List<string>();
                }

                var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);
                var contentText = responseObject?.Choices?.FirstOrDefault()?.Message?.Content;

                // Basic line-splitting
                var questions = contentText?
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(q => q.TrimStart('-', '*', ' ', '0', '.', '1', '2', '3', '4', '5', '6', '7', '8', '9')) // remove numbering
                    .Where(q => !string.IsNullOrWhiteSpace(q))
                    .ToList();

                return questions ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI for interview questions");
                return new List<string>();
            }
        }

    }
}