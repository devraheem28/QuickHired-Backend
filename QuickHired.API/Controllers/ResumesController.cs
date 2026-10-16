using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using QuickHired.BOL;
using QuickHired.DAL.Data;
using SendGrid.Helpers.Mail;
using System.Text.Json.Serialization;
using static QuickHired.API.Controllers.ResumesController;

namespace QuickHired.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResumesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ResumesController> _logger;

        public ResumesController(AppDbContext db, ILogger<ResumesController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // POST: /api/resumes
        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] SaveResumeRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.ResumeName))
                return BadRequest(new { error = "UserId and ResumeName are required" });

            Resume? existingResume = null;

            if (request.Id != Guid.Empty)
            {
                existingResume = await _db.Resumes
                    .Include(r => r.Experience)
                    .Include(r => r.Education)
                    .Include(r => r.Projects)
                    .Include(r => r.Certifications)
                    .Include(r => r.Languages)
                    .Include(r => r.Skills)
                    .FirstOrDefaultAsync(r => r.Id == request.Id);
            }

            if (existingResume != null)
            {
                // update case
                existingResume.ResumeName = request.ResumeName;
                existingResume.FullName = request.Personal.FullName;
                existingResume.Email = request.Personal.Email;
                existingResume.Phone = request.Personal.Phone;
                existingResume.Location = request.Personal.Location;

                existingResume.LinkedIn = request.Personal.LinkedIn;
                existingResume.Portfolio = request.Personal.Portfolio;
                existingResume.Summary = request.Personal.Summary;
                existingResume.UpdatedUtc = DateTime.UtcNow;

                // clear old children
                _db.Experiences.RemoveRange(existingResume.Experience);
                _db.Educations.RemoveRange(existingResume.Education);
                _db.Projects.RemoveRange(existingResume.Projects);
                _db.Certifications.RemoveRange(existingResume.Certifications);
                _db.Languages.RemoveRange(existingResume.Languages);
                _db.Skills.RemoveRange(existingResume.Skills);

                // map new children
                var updatedEntity = request.ToEntity();
                foreach (var e in updatedEntity.Experience) { e.Resume = existingResume; _db.Experiences.Add(e); }
                foreach (var e in updatedEntity.Education) { e.Resume = existingResume; _db.Educations.Add(e); }
                foreach (var p in updatedEntity.Projects) { p.Resume = existingResume; _db.Projects.Add(p); }
                foreach (var c in updatedEntity.Certifications) { c.Resume = existingResume; _db.Certifications.Add(c); }
                foreach (var l in updatedEntity.Languages) { l.Resume = existingResume; _db.Languages.Add(l); }
                foreach (var s in updatedEntity.Skills) { s.Resume = existingResume; _db.Skills.Add(s); }
                _db.Entry(existingResume).State = EntityState.Modified;
                await _db.SaveChangesAsync();
                return Ok(new { id = existingResume.Id, updated = true });
            }
            else
            {
                //  create case
                var entity = request.ToEntity();
                entity.CreatedUtc = DateTime.UtcNow;
                entity.UpdatedUtc = DateTime.UtcNow;

                foreach (var e in entity.Experience) e.Resume = entity;
                foreach (var e in entity.Education) e.Resume = entity;
                foreach (var p in entity.Projects) p.Resume = entity;
                foreach (var c in entity.Certifications) c.Resume = entity;
                foreach (var l in entity.Languages) l.Resume = entity;
                foreach (var s in entity.Skills) s.Resume = entity;

                _db.Resumes.Add(entity);
                await _db.SaveChangesAsync();

                return Ok(new { id = entity.Id, created = true });
            }
        }


        // GET: /api/resumes/by-user/{userId}
        [HttpGet("by-user/{userId}")]
        public async Task<IActionResult> GetByUser(string userId)
        {
            var list = await _db.Resumes
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.UpdatedUtc)
                .Select(r => new { r.Id, r.ResumeName, r.CreatedUtc, r.UpdatedUtc })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/resumes/{id}
        //[HttpGet("{id:guid}")]
        //public async Task<IActionResult> Get(Guid id)
        //{
        //    var resume = await _db.Resumes
        //        .Include(r => r.Experience)
        //        .Include(r => r.Education)
        //        .Include(r => r.Projects)
        //        .Include(r => r.Certifications)
        //        .Include(r => r.Languages)
        //        .Include(r => r.Skills)
        //        .FirstOrDefaultAsync(r => r.Id == id);

        //    if (resume == null) return NotFound();

        //    return Ok(resume);
        //}

        [HttpGet("{id}")]
        public async Task<ActionResult<ResumeResponse>> GetResume(Guid id)
        {
            var resume = await _db.Resumes
                .Include(r => r.Experience)
                        .Include(r => r.Education)
                        .Include(r => r.Projects)
                        .Include(r => r.Certifications)
                        .Include(r => r.Languages)
                        .Include(r => r.Skills)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (resume == null) return NotFound();

            var response = new ResumeResponse
            {
                Id = resume.Id,
                ResumeName = resume.ResumeName,
                UserId = resume.UserId,
                Personal = new PersonalDto
                {
                    FullName = resume.FullName,
                    Email = resume.Email,
                    Phone = resume.Phone,
                    Location = resume.Location,
                    LinkedIn = resume.LinkedIn,
                    Portfolio = resume.Portfolio,
                    Summary = resume.Summary
                },
                Experience = resume.Experience.Select(e => new ExperienceDto
                {
                    Id = e.Id.ToString(),
                    Company = e.Company,
                    Position = e.Position,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Current = e.Current,
                    Description = e.Description
                }).ToList(),
                Education = resume.Education.Select(ed => new EducationDto
                {
                    Id = ed.Id.ToString(),
                    Institution = ed.Institution,
                    Degree = ed.Degree,
                    Field = ed.Field,
                    GraduationDate = ed.GraduationDate,
                    Gpa = ed.Gpa
                }).ToList(),
                Projects = resume.Projects.Select(p => new ProjectDto
                {
                    Id = p.Id.ToString(),
                    Name = p.Name,
                    Description = p.Description,
                    Technologies = p.Technologies,
                    Link = p.Link
                }).ToList(),
                Skills = resume.Skills.Select(s => s.Name).ToList(),
                Certifications = resume.Certifications.Select(c => c.Name).ToList(),
                Languages = resume.Languages.Select(l => l.Name).ToList()
            };

            return Ok(response);
        }

        public class SaveResumeRequest
        {
            public Guid Id { get; set; }
            public string ResumeName { get; set; } = default!;
            public string UserId { get; set; } = default!;

            public PersonalDto Personal { get; set; } = new();
            public List<ExperienceDto> Experience { get; set; } = new();
            public List<EducationDto> Education { get; set; } = new();
            public List<string> Skills { get; set; } = new();
            public List<ProjectDto> Projects { get; set; } = new();
            public List<string> Certifications { get; set; } = new();
            public List<string> Languages { get; set; } = new();
        }

        public class PersonalDto
        {
            public string FullName { get; set; } = "";
            public string Email { get; set; } = "";
            public string Phone { get; set; } = "";
            public string Location { get; set; } = "";
            public string? LinkedIn { get; set; }
            public string? Portfolio { get; set; }
            public string Summary { get; set; } = "";
        }

        public class ExperienceDto
        {
            public string Id { get; set; } = "";
            public string Company { get; set; } = "";
            public string Position { get; set; } = "";
            public string StartDate { get; set; } = "";
            public string? EndDate { get; set; }
            public bool Current { get; set; }
            public string Description { get; set; } = "";
        }

        public class EducationDto
        {
            public string Id { get; set; } = "";
            public string Institution { get; set; } = "";
            public string Degree { get; set; } = "";
            public string Field { get; set; } = "";
            public string GraduationDate { get; set; } = "";
            public string? Gpa { get; set; }
        }

        public class ProjectDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Technologies { get; set; } = "";
            public string? Link { get; set; }
        }
        public class ResumeResponse
        {
            public Guid Id { get; set; }
            public string ResumeName { get; set; } = default!;
            public string UserId { get; set; } = default!;

            [JsonPropertyName("personalInfo")]
            public PersonalDto Personal { get; set; } = new();
            public List<ExperienceDto> Experience { get; set; } = new();
            public List<EducationDto> Education { get; set; } = new();
            public List<string> Skills { get; set; } = new();
            public List<ProjectDto> Projects { get; set; } = new();
            public List<string> Certifications { get; set; } = new();
            public List<string> Languages { get; set; } = new();
        }


    }
    public static class ResumeMapper
    {
        public static Resume ToEntity(this SaveResumeRequest dto)
        {
            var resume = new Resume
            {
                Id = Guid.NewGuid(),
                ResumeName = dto.ResumeName,
                UserId = dto.UserId,

                FullName = dto.Personal.FullName,
                Email = dto.Personal.Email,
                Phone = dto.Personal.Phone,
                Location = dto.Personal.Location,
                LinkedIn = dto.Personal.LinkedIn,
                Portfolio = dto.Personal.Portfolio,
                Summary = dto.Personal.Summary,
            };

            resume.Experience = dto.Experience.Select(x => new Experience
            {
                Id = Guid.NewGuid(),
                Company = x.Company,
                Position = x.Position,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Current = x.Current,
                Description = x.Description
            }).ToList();

            resume.Education = dto.Education.Select(x => new Education
            {
                Id = Guid.NewGuid(),
                Institution = x.Institution,
                Degree = x.Degree,
                Field = x.Field,
                GraduationDate = x.GraduationDate,
                Gpa = x.Gpa
            }).ToList();

            resume.Projects = dto.Projects.Select(x => new Project
            {
                Id = Guid.NewGuid(),
                Name = x.Name,
                Description = x.Description,
                Technologies = x.Technologies,
                Link = x.Link
            }).ToList();

            resume.Certifications = dto.Certifications.Select(c => new Certification
            {
                Id = Guid.NewGuid(),
                Name = c
            }).ToList();

            resume.Languages = dto.Languages.Select(l => new LanguageItem
            {
                Id = Guid.NewGuid(),
                Name = l
            }).ToList();

            resume.Skills = dto.Skills.Select(s => new SkillItem
            {
                Id = Guid.NewGuid(),
                Name = s
            }).ToList();

            return resume;
        }
    }


}
