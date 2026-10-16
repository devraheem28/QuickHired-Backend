using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuickHired.API.DTOs;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamsController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TeamsController(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }


        [HttpPost("create-teams-meeting")]
        public async Task<IActionResult> CreateTeamsMeeting([FromBody] TeamsMeetingDTO request)
        {
            string accessToken = await GetTeamsAccessToken();
            string userId = "3d76c499-a452-4c20-ac8f-67c49e403c9c"; // Azure AD User ID

            var meetingData = new
            {
                subject = request.Topic,
                startDateTime = request.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDateTime = request.StartTime.AddMinutes(request.Duration).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                isEntryExitAnnounced = false
            };

            var content = new StringContent(JsonSerializer.Serialize(meetingData), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PostAsync($"https://graph.microsoft.com/v1.0/users/{userId}/onlineMeetings", content);

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, responseString);
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseString);
            string joinUrl = jsonResponse.GetProperty("joinWebUrl").GetString();

            return Ok(new { MeetingLink = joinUrl });
        }


        [HttpGet("get-meeting/{meetingId}")]
        public async Task<IActionResult> GetMeeting(string meetingId)
        {
            string accessToken = await GetTeamsAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync($"https://graph.microsoft.com/v1.0/me/onlineMeetings/{meetingId}");

            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, responseString);

            return Ok(JsonSerializer.Deserialize<JsonElement>(responseString));
        }
        private async Task<string> GetUserId(string email)
        {
            string accessToken = await GetTeamsAccessToken();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"https://graph.microsoft.com/v1.0/users/{email}");
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get User ID. Response: {responseString}");
            }

            var json = JsonSerializer.Deserialize<JsonElement>(responseString);
            return json.GetProperty("id").GetString();
        }


        [HttpPatch("update-meeting/{meetingId}")]
        public async Task<IActionResult> UpdateMeeting(string meetingId, [FromBody] TeamsMeetingDTO request)
        {
            string accessToken = await GetTeamsAccessToken();
            var updateData = new
            {
                subject = request.Topic,
                startDateTime = request.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endDateTime = request.StartTime.AddMinutes(request.Duration).ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var content = new StringContent(JsonSerializer.Serialize(updateData), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PatchAsync($"https://graph.microsoft.com/v1.0/me/onlineMeetings/{meetingId}", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, responseString);

            return Ok(JsonSerializer.Deserialize<JsonElement>(responseString));
        }

        [HttpDelete("delete-meeting/{meetingId}")]
        public async Task<IActionResult> DeleteMeeting(string meetingId)
        {
            string accessToken = await GetTeamsAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/onlineMeetings/{meetingId}");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Failed to delete the meeting.");

            return Ok("Meeting deleted successfully.");
        }

        [HttpGet("list-meetings")]
        public async Task<IActionResult> ListMeetings()
        {
            string accessToken = await GetTeamsAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/onlineMeetings");
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, responseString);

            return Ok(JsonSerializer.Deserialize<JsonElement>(responseString));
        }

        private async Task<string> GetTeamsAccessToken()
        {
            string tenantId = _configuration["Teams:TenantId"];
            string clientId = _configuration["Teams:ClientId"];
            string clientSecret = _configuration["Teams:ClientSecret"];

            var client = new HttpClient();
            var values = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "scope", "https://graph.microsoft.com/.default" }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve Teams Access Token. Response: {await response.Content.ReadAsStringAsync()}");
            }

            var responseData = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(responseData);
            return json.GetProperty("access_token").GetString();
        }

    }
}
