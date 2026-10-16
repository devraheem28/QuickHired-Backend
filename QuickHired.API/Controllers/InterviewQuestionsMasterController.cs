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
using QuickHired.DAL;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InterviewQuestionsMasterController : ControllerHandler
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<InterviewQuestionsMasterController> _logger;

        public InterviewQuestionsMasterController(AppDbContext context, IMapper mapper, ILogger<InterviewQuestionsMasterController> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public async Task<ApiResponse<List<InterviewQuestionsDTO>>> GetInterviewQuestions()
        {
            return await DoTryAsync(async () =>
            {
                var questions = await _context.InterviewQuestions.ToListAsync();
                return _mapper.Map<List<InterviewQuestionsDTO>>(questions);
            }, nameof(InterviewQuestionsMasterController), nameof(GetInterviewQuestions), _logger);
        }

        [Authorize]
        [HttpPost]
        public async Task<ApiResponse<InterviewQuestionsDTO>> CreateInterviewQuestion(InterviewQuestionsDTO questionDto)
        {
            return await DoTryAsync(async () =>
            {
                var question = _mapper.Map<InterviewQuestions>(questionDto);
                _context.InterviewQuestions.Add(question);
                await _context.SaveChangesAsync();
                return _mapper.Map<InterviewQuestionsDTO>(question);
            }, nameof(InterviewQuestionsMasterController), nameof(CreateInterviewQuestion), _logger);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ApiResponse<InterviewQuestionsDTO>> GetInterviewQuestionById(int id)
        {
            return await DoTryAsync(async () =>
            {
                var question = await _context.InterviewQuestions.FindAsync(id);
                if (question == null) throw new KeyNotFoundException("Interview question not found");
                return _mapper.Map<InterviewQuestionsDTO>(question);
            }, nameof(InterviewQuestionsMasterController), nameof(GetInterviewQuestionById), _logger);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ApiResponse<InterviewQuestionsDTO>> UpdateInterviewQuestion(int id, InterviewQuestionsDTO questionDto)
        {
            return await DoTryAsync(async () =>
            {
                var question = await _context.InterviewQuestions.FindAsync(id);
                if (question == null) throw new KeyNotFoundException("Interview question not found");

                _mapper.Map(questionDto, question);
                _context.InterviewQuestions.Update(question);
                await _context.SaveChangesAsync();
                return _mapper.Map<InterviewQuestionsDTO>(question);
            }, nameof(InterviewQuestionsMasterController), nameof(UpdateInterviewQuestion), _logger);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ApiResponse<string>> DeleteInterviewQuestion(int id)
        {
            return await DoTryAsync(async () =>
            {
                var question = await _context.InterviewQuestions.FindAsync(id);
                if (question == null) throw new KeyNotFoundException("Interview question not found");

                _context.InterviewQuestions.Remove(question);
                await _context.SaveChangesAsync();
                return "Interview question deleted successfully";
            }, nameof(InterviewQuestionsMasterController), nameof(DeleteInterviewQuestion), _logger);
        }
    }
}
