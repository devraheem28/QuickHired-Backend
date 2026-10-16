using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using QuickHired.API.Models;
using Microsoft.Extensions.Logging;

namespace QuickHired.API.Services
{
    /// <summary>
    /// Service for parsing and analyzing resumes (PDF, DOCX, TXT)
    /// Extracts skills, experience, education, and contact information
    /// </summary>
    public class ResumeParserService
    {
        private readonly ILogger<ResumeParserService> _logger;

        // Common skills database
        private static readonly List<string> CommonSkills = new()
        {
            // Programming Languages
            "C#", "JavaScript", "Python", "Java", "TypeScript", "C++", "PHP", "Ruby", "Go", "Rust",
            "Swift", "Kotlin", "R", "MATLAB", "Scala", "Groovy", "Perl", "Lua", "Objective-C",

            // Web Technologies
            "HTML", "CSS", "React", "Angular", "Vue", "Node.js", "Express", "ASP.NET", "Django",
            "Flask", "Spring", "Laravel", "Rails", "Next.js", "Nuxt", "WebAssembly", "GraphQL",

            // Databases
            "SQL Server", "MySQL", "PostgreSQL", "MongoDB", "Redis", "Elasticsearch", "Cassandra",
            "Oracle", "MariaDB", "DynamoDB", "Firebase", "Cosmos DB", "SQLite",

            // Cloud & DevOps
            "AWS", "Azure", "Google Cloud", "Docker", "Kubernetes", "Jenkins", "GitLab CI/CD",
            "GitHub Actions", "Terraform", "CloudFormation", "Heroku", "DigitalOcean",

            // Tools & Frameworks
            "Git", "GitHub", "GitLab", "Bitbucket", "Jenkins", "JIRA", "Confluence", "Slack",
            "Agile", "Scrum", "Kanban", "REST API", "SOAP", "XML", "JSON", "OAuth",

            // Data & Analytics
            "Power BI", "Tableau", "Excel", "Data Analysis", "Machine Learning", "TensorFlow",
            "PyTorch", "Scikit-learn", "Pandas", "NumPy", "Big Data", "Hadoop", "Spark",

            // Other
            "Linux", "Windows", "macOS", "IoT", "API Development", "Microservices", "Testing",
            "Unit Testing", "Integration Testing", "Performance Optimization", "Security"
        };

        public ResumeParserService(ILogger<ResumeParserService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Parse resume from PDF content
        /// </summary>
        public async Task<ResumeDataDto> ParseResumeAsync(byte[] fileContent, string fileExtension)
        {
            try
            {
                _logger.LogInformation($"Parsing resume with extension: {fileExtension}");

                string text = fileExtension.ToLower() switch
                {
                    ".pdf" => ExtractTextFromPdf(fileContent),
                    ".txt" => System.Text.Encoding.UTF8.GetString(fileContent),
                    ".docx" => ExtractTextFromDocx(fileContent),
                    _ => throw new InvalidOperationException($"Unsupported file type: {fileExtension}")
                };

                var resumeData = ExtractResumeData(text);
                _logger.LogInformation($"Extracted {resumeData.Skills.Count} skills from resume");

                return resumeData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing resume");
                throw;
            }
        }

        /// <summary>
        /// Match resume skills to job requirements
        /// Returns match percentage and matched/missing skills
        /// </summary>
        public async Task<JobMatchDto> MatchResumeToJobAsync(ResumeDataDto resume, JobListingDto job)
        {
            try
            {
                // Extract required skills from job description
                var requiredSkills = ExtractSkillsFromText(job.Description ?? "");
                requiredSkills.AddRange(job.Skills?.ToList() ?? new List<string>());
                requiredSkills = requiredSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // Find matched skills
                var matchedSkills = resume.Skills
                    .Where(s => requiredSkills.Any(r => r.Equals(s, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Find missing skills
                var missingSkills = requiredSkills
                    .Where(r => !resume.Skills.Any(s => s.Equals(r, StringComparison.OrdinalIgnoreCase)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Calculate match percentage
                int matchPercentage = requiredSkills.Count > 0
                    ? (int)((matchedSkills.Count / (decimal)requiredSkills.Count) * 100)
                    : 0;

                // Generate suggestions
                var suggestions = GenerateSuggestions(resume, job, missingSkills, matchPercentage);

                var result = new JobMatchDto
                {
                    Job = job,
                    MatchPercentage = matchPercentage,
                    MatchedSkills = matchedSkills,
                    MissingSkills = missingSkills,
                    Suggestions = suggestions
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error matching resume to job");
                throw;
            }
        }

        /// <summary>
        /// Customize resume for a specific job by emphasizing relevant skills
        /// </summary>
        public async Task<string> CustomizeResumeForJobAsync(ResumeDataDto resume, JobListingDto job)
        {
            try
            {
                var jobSkills = ExtractSkillsFromText(job.Description ?? "");
                jobSkills.AddRange(job.Skills?.ToList() ?? new List<string>());

                // Reorder resume sections to prioritize matching skills
                var customizedContent = ReorderResumeSections(resume, jobSkills);

                // Add keyword optimization suggestions
                customizedContent += "\n\n--- OPTIMIZATION SUGGESTIONS ---\n";
                customizedContent += "Consider adding these keywords from the job description to your resume:\n";

                var missingSkills = jobSkills
                    .Where(s => !resume.Skills.Any(rs => rs.Equals(s, StringComparison.OrdinalIgnoreCase)))
                    .Take(5);

                foreach (var skill in missingSkills)
                {
                    customizedContent += $"• {skill}\n";
                }

                return customizedContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error customizing resume");
                throw;
            }
        }

        /// <summary>
        /// Extract text from PDF
        /// </summary>
        private string ExtractTextFromPdf(byte[] pdfContent)
        {
            try
            {
                var text = new System.Text.StringBuilder();

                using (var reader = new PdfReader(pdfContent))
                {
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                        text.Append("\n");
                    }
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw;
            }
        }

        /// <summary>
        /// Extract text from DOCX (placeholder - would need additional library)
        /// </summary>
        private string ExtractTextFromDocx(byte[] docxContent)
        {
            // Note: For real implementation, use OpenXML library
            // For now, return placeholder
            _logger.LogWarning("DOCX extraction not fully implemented - requires OpenXML library");
            return "Resume content from DOCX";
        }

        /// <summary>
        /// Extract skills and sections from resume text
        /// </summary>
        private ResumeDataDto ExtractResumeData(string resumeText)
        {
            var resume = new ResumeDataDto
            {
                RawContent = resumeText,
                Skills = ExtractSkillsFromText(resumeText),
                Email = ExtractEmail(resumeText),
                Phone = ExtractPhone(resumeText),
                ExperienceYears = ExtractExperienceYears(resumeText),
                Education = ExtractEducation(resumeText)
            };

            return resume;
        }

        /// <summary>
        /// Extract skills from text by matching against common skills list
        /// </summary>
        private List<string> ExtractSkillsFromText(string text)
        {
            var foundSkills = new List<string>();

            foreach (var skill in CommonSkills)
            {
                // Case-insensitive search with word boundary
                var pattern = $@"\b{Regex.Escape(skill)}\b";
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    foundSkills.Add(skill);
                }
            }

            return foundSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Extract email address from text
        /// </summary>
        private string? ExtractEmail(string text)
        {
            var match = Regex.Match(text, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            return match.Success ? match.Value : null;
        }

        /// <summary>
        /// Extract phone number from text
        /// </summary>
        private string? ExtractPhone(string text)
        {
            // Match various phone formats
            var patterns = new[]
            {
                @"\+?1?\s*\(?([0-9]{3})\)?[\s.-]?([0-9]{3})[\s.-]?([0-9]{4})",
                @"\+\d{1,3}\s?\d{1,14}",
                @"\d{3}[-.\s]?\d{3}[-.\s]?\d{4}"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success)
                    return match.Value;
            }

            return null;
        }

        /// <summary>
        /// Extract years of experience from resume text
        /// </summary>
        private int ExtractExperienceYears(string text)
        {
            // Look for patterns like "5 years", "5+", etc.
            var matches = Regex.Matches(text, @"(\d+)\+?\s*years");

            if (matches.Count == 0)
                return 0;

            // Get the highest number found
            var years = matches
                .Cast<Match>()
                .Select(m => int.Parse(m.Groups[1].Value))
                .OrderByDescending(y => y)
                .FirstOrDefault();

            return years;
        }

        /// <summary>
        /// Extract education information
        /// </summary>
        private List<string> ExtractEducation(string text)
        {
            var degrees = new List<string>();
            var degreePatterns = new[] { "bachelor", "master", "phd", "associate", "diploma", "certificate" };

            foreach (var degree in degreePatterns)
            {
                if (text.Contains(degree, StringComparison.OrdinalIgnoreCase))
                {
                    degrees.Add(degree);
                }
            }

            return degrees;
        }

        /// <summary>
        /// Reorder resume sections to prioritize job-relevant content
        /// </summary>
        private string ReorderResumeSections(ResumeDataDto resume, List<string> jobSkills)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=== CUSTOMIZED RESUME FOR THIS POSITION ===\n");
            sb.AppendLine("Contact Information:");
            if (!string.IsNullOrEmpty(resume.Email))
                sb.AppendLine($"Email: {resume.Email}");
            if (!string.IsNullOrEmpty(resume.Phone))
                sb.AppendLine($"Phone: {resume.Phone}");

            sb.AppendLine($"\nExperience: {resume.ExperienceYears} years");

            sb.AppendLine($"\nKey Skills (for this position):");
            var relevantSkills = resume.Skills
                .Where(s => jobSkills.Any(js => js.Equals(s, StringComparison.OrdinalIgnoreCase)))
                .Take(10);

            foreach (var skill in relevantSkills)
            {
                sb.AppendLine($"• {skill}");
            }

            if (resume.Education.Count > 0)
            {
                sb.AppendLine($"\nEducation: {string.Join(", ", resume.Education)}");
            }

            sb.AppendLine($"\nTotal Skills: {resume.Skills.Count}");

            return sb.ToString();
        }

        /// <summary>
        /// Generate suggestions for resume improvement
        /// </summary>
        private List<string> GenerateSuggestions(
            ResumeDataDto resume,
            JobListingDto job,
            List<string> missingSkills,
            int matchPercentage)
        {
            var suggestions = new List<string>();

            if (matchPercentage < 50)
            {
                suggestions.Add("Your skill match is below 50%. Consider gaining experience in the required technologies.");
            }
            else if (matchPercentage < 70)
            {
                suggestions.Add("You have moderate skill match. Highlight your relevant experience in the cover letter.");
            }
            else
            {
                suggestions.Add("Excellent skill match! You're well-qualified for this position.");
            }

            if (missingSkills.Count > 0 && missingSkills.Count <= 3)
            {
                suggestions.Add($"Consider learning {string.Join(", ", missingSkills.Take(3))} to strengthen your candidacy.");
            }

            if (resume.ExperienceYears < 2)
            {
                suggestions.Add("Your experience level is junior - emphasize projects and learning enthusiasm.");
            }
            else if (resume.ExperienceYears >= 5)
            {
                suggestions.Add("Your extensive experience is valuable - lead with your most relevant achievements.");
            }

            return suggestions;
        }
    }
}
