using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuickHired.API.DTOs;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GoogleMeetController : ControllerBase
    {
        private async Task<CalendarService> GetCalendarServiceAsync()
        {
            using var stream = new FileStream("wwwroot/credentials.json", FileMode.Open, FileAccess.Read);

            var credential = await GoogleCredential.FromStreamAsync(stream, CancellationToken.None);
            credential = credential.CreateScoped(CalendarService.Scope.Calendar);

            return new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Google Meet API Integration"
            });
        }



        [HttpPost("create-meeting")]
        public async Task<IActionResult> CreateGoogleMeetMeeting([FromBody] GoogleMeetRequestDTO request)
        {
            var service = await GetCalendarServiceAsync();

            var meeting = new Event
            {
                Summary = request.Topic,
                Start = new EventDateTime { DateTime = request.StartTime, TimeZone = "UTC" },
                End = new EventDateTime { DateTime = request.StartTime.AddMinutes(request.Duration), TimeZone = "UTC" },
                ConferenceData = new ConferenceData
                {
                    CreateRequest = new CreateConferenceRequest
                    {
                        RequestId = Guid.NewGuid().ToString(), // Unique request ID
                        ConferenceSolutionKey = new ConferenceSolutionKey { Type = "hangoutsMeet" }
                    }
                },
                Attendees = new List<EventAttendee>
                {
                    new EventAttendee { Email = request.ParticipantEmail }
                }
            };

            var calendarId = "primary";
            var requestInsert = service.Events.Insert(meeting, calendarId);
            requestInsert.ConferenceDataVersion = 1; // Enable Google Meet link generation

            var createdEvent = await requestInsert.ExecuteAsync();

            var meetLink = createdEvent.ConferenceData?.EntryPoints?[0]?.Uri;
            return Ok(new { meetingLink = meetLink });
        }
    }
}
