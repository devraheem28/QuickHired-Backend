using System.ComponentModel.DataAnnotations;

namespace QuickHired.API.DTOs
{
    public class RegisterDTO
    {
        public string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the last name of the user.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Gets or sets the user's job title.
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the user's job title.
        /// </summary>
        public string JobTitle { get; set; }

        /// <summary>
        /// Gets or sets the experience level (e.g., Junior, Mid, Senior).
        /// </summary>
        public string ExperienceLevel { get; set; }

        /// <summary>
        /// Gets or sets the number of years of experience.
        /// </summary>
        public int YearsOfExperience { get; set; }

        /// <summary>
        /// Gets or sets the user's skills.
        /// </summary>
        public string Skills { get; set; }

        /// <summary>
        /// Gets or sets the user's educational background.
        /// </summary>
        public string Education { get; set; }

        /// <summary>
        /// Gets or sets the LinkedIn profile URL.
        /// </summary>
        public string LinkedInProfile { get; set; }

        /// <summary>
        /// Gets or sets the resume file URL.
        /// </summary>
        public string ResumeUrl { get; set; }

        /// <summary>
        /// Gets or sets the user's address.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the city of the user.
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// Gets or sets the state of the user.
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the country of the user.
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        /// Gets or sets the user's ZIP code.
        /// </summary>
        public string ZipCode { get; set; }

        [RegularExpression("(?=^.{6,10}$)(?=.*\\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[!@#$%^&amp;*()_+}{&quot;:;'?/&gt;.&lt;,])(?!.*\\s).*$",
        ErrorMessage = "Password must contain 1 Uppercase, 1 Lowercase, 1 number, 1 non alphanumeric and atleast 6 characters.")]
        public required string Password { get; set; }
    }
}
