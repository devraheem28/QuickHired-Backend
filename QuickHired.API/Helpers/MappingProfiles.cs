using AutoMapper;
using QuickHired.API.DTOs;
using QuickHired.BOL;

namespace QuickHired.API.Helpers
{
    public class MappingProfiles : Profile
    {
        public MappingProfiles()
        {
            // Meetings Mapping
            CreateMap<MeetingDTO, Meetings>()
                .ForMember(dest => dest.InterviewQuestions, opt => opt.Ignore());

            CreateMap<Meetings, MeetingDTO>();

            // Interview Questions Mapping
            CreateMap<InterviewQuestionsDTO, InterviewQuestions>()
                .ForMember(dest => dest.Meetings, opt => opt.Ignore());
            CreateMap<InterviewQuestions, InterviewQuestionsDTO>();
        }
    }
}
