using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuickHired.API.DTOs;
using QuickHired.API.Interfaces.Services;
using QuickHired.DAL.Data;
using QuickHired.BOL;
using System.Security.Claims;
using QuickHired.API.Models;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MeetingsMasterController : ControllerHandler
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<MeetingsMasterController> _logger;

        public MeetingsMasterController(AppDbContext context, IMapper mapper, ILogger<MeetingsMasterController> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public async Task<ApiResponse<List<MeetingDTO>>> GetMeetings()
        {
            return await DoTryAsync(async () =>
            {
                var meetings = await _context.Meetings.ToListAsync();
                return _mapper.Map<List<MeetingDTO>>(meetings);
            }, nameof(MeetingsMasterController), nameof(GetMeetings), _logger);
        }

        [Authorize]
        [HttpPost]
        public async Task<ApiResponse<MeetingDTO>> CreateMeeting(MeetingDTO meetingDto)
        {
            return await DoTryAsync(async () =>
            {
                var meeting = _mapper.Map<Meetings>(meetingDto);
                _context.Meetings.Add(meeting);
                await _context.SaveChangesAsync();
                return _mapper.Map<MeetingDTO>(meeting);
            }, nameof(MeetingsMasterController), nameof(CreateMeeting), _logger);
        }
        //public async Task<ApiResponse<MeetingDTO>> InsertMeeting(MeetingDTO meetingDto)
        //{
        //    var meeting = _mapper.Map<Meetings>(meetingDto);
        //    _context.Meetings.Add(meeting);
        //    await _context.SaveChangesAsync();
        //    return _mapper.Map<MeetingDTO>(meeting);
        //}


        [Authorize]
        [HttpGet("{id}")]
        public async Task<ApiResponse<MeetingDTO>> GetMeetingById(int id)
        {
            return await DoTryAsync(async () =>
            {
                var meeting = await _context.Meetings.FindAsync(id);
                if (meeting == null) throw new KeyNotFoundException("Meeting not found");
                return _mapper.Map<MeetingDTO>(meeting);
            }, nameof(MeetingsMasterController), nameof(GetMeetingById), _logger);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ApiResponse<MeetingDTO>> UpdateMeeting(int id, MeetingDTO meetingDto)
        {
            return await DoTryAsync(async () =>
            {
                var meeting = await _context.Meetings.FindAsync(id);
                if (meeting == null) throw new KeyNotFoundException("Meeting not found");

                _mapper.Map(meetingDto, meeting);
                _context.Meetings.Update(meeting);
                await _context.SaveChangesAsync();
                return _mapper.Map<MeetingDTO>(meeting);
            }, nameof(MeetingsMasterController), nameof(UpdateMeeting), _logger);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ApiResponse<string>> DeleteMeeting(int id)
        {
            return await DoTryAsync(async () =>
            {
                var meeting = await _context.Meetings.FindAsync(id);
                if (meeting == null) throw new KeyNotFoundException("Meeting not found");

                _context.Meetings.Remove(meeting);
                await _context.SaveChangesAsync();
                return "Meeting deleted successfully";
            }, nameof(MeetingsMasterController), nameof(DeleteMeeting), _logger);
        }
    }
}
