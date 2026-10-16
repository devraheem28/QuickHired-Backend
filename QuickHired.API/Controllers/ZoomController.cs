using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using QuickHired.API.DTOs;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ZoomController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ZoomController(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        [HttpPost("create-meeting")]
        public async Task<IActionResult> CreateZoomMeeting([FromBody] MeetingRequestDTO request)
        {
            string accessToken = await GetZoomAccessToken();

            var zoomMeeting = new
            {
                topic = request.Topic,
                type = 2,
                start_time = request.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                duration = request.Duration,
                timezone = "UTC",
                settings = new { host_video = true, participant_video = true }
            };

            var content = new StringContent(JsonSerializer.Serialize(zoomMeeting), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PostAsync("https://api.zoom.us/v2/users/me/meetings", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, responseString);
            }

            var json = JsonSerializer.Deserialize<JsonElement>(responseString);
            string meetingLink = json.GetProperty("join_url").GetString();

            return Ok(new { MeetingLink = meetingLink });
        }

        [HttpGet("get-meeting/{meetingId}")]
        public async Task<IActionResult> GetZoomMeeting(string meetingId)
        {
            string accessToken = await GetZoomAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync($"https://api.zoom.us/v2/meetings/{meetingId}");
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, responseString);
            }

            return Ok(JsonSerializer.Deserialize<JsonElement>(responseString));
        }

        [HttpPatch("update-meeting/{meetingId}")]
        public async Task<IActionResult> UpdateZoomMeeting(string meetingId, [FromBody] MeetingRequestDTO request)
        {
            string accessToken = await GetZoomAccessToken();

            var zoomMeetingUpdate = new
            {
                topic = request.Topic,
                start_time = request.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                duration = request.Duration,
                settings = new { host_video = true, participant_video = true }
            };

            var content = new StringContent(JsonSerializer.Serialize(zoomMeetingUpdate), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.PatchAsync($"https://api.zoom.us/v2/meetings/{meetingId}", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, responseString);
            }

            return Ok(JsonSerializer.Deserialize<JsonElement>(responseString));
        }

        [HttpDelete("delete-meeting/{meetingId}")]
        public async Task<IActionResult> DeleteZoomMeeting(string meetingId)
        {
            string accessToken = await GetZoomAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.DeleteAsync($"https://api.zoom.us/v2/meetings/{meetingId}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, await response.Content.ReadAsStringAsync());
            }

            return Ok(new { message = "Meeting deleted successfully." });
        }

        [HttpGet("list-meetings")]
        public async Task<IActionResult> ListZoomMeetings()
        {
            string accessToken = await GetZoomAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync("https://api.zoom.us/v2/users/me/meetings");
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, responseString);
            }
            return Ok(JsonSerializer.Deserialize<JsonElement>(responseString));
        }

        private async Task<string> GetZoomAccessToken()
        {
            string accountId = _configuration["Zoom:AccountId"];
            string clientId = _configuration["Zoom:ClientId"];
            string clientSecret = _configuration["Zoom:ClientSecret"];

            var client = new HttpClient();
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var content = new StringContent($"grant_type=account_credentials&account_id={accountId}", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await client.PostAsync("https://zoom.us/oauth/token", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve Zoom Access Token. Response: {await response.Content.ReadAsStringAsync()}");
            }

            var responseData = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(responseData);
            return json.GetProperty("access_token").GetString();
        }
    }
}
