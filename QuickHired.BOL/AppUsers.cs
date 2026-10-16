using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace QuickHired.BOL
{
    /// <summary>
    /// Represents an application user with extended profile details.
    /// </summary>
    [Table("AppUsers")]
    public class AppUsers : IdentityUser
    {
        /// <summary>
        /// Gets or sets the first name of the user.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the last name of the user.
        /// </summary>
        public string LastName { get; set; }

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
        public string? ProfileImagePath { get; set; }

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



        /// <summary>
        /// Navigation property for the meetings associated with the user.
        /// </summary>
        public virtual ICollection<Meetings> Meetings { get; set; } = new List<Meetings>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AppUsers"/> class.
        /// </summary>
        public AppUsers()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppUsers"/> class with specific values.
        /// </summary>
        /// <param name="firstName">User's first name.</param>
        /// <param name="lastName">User's last name.</param>
        /// <param name="jobTitle">User's job title.</param>
        /// <param name="experienceLevel">User's experience level.</param>
        /// <param name="yearsOfExperience">User's years of experience.</param>
        /// <param name="skills">User's skills.</param>
        /// <param name="education">User's education details.</param>
        /// <param name="linkedInProfile">User's LinkedIn profile URL.</param>
        /// <param name="resumeUrl">User's resume URL.</param>
        /// <param name="address">User's address.</param>
        /// <param name="city">User's city.</param>
        /// <param name="state">User's state.</param>
        /// <param name="country">User's country.</param>
        /// <param name="zipCode">User's ZIP code.</param>
        public AppUsers(string firstName, string lastName, string jobTitle, string experienceLevel,
                        int yearsOfExperience, string skills, string education, string linkedInProfile,
                        string resumeUrl, string address, string city, string state, string country, string zipCode)
        {
            FirstName = firstName;
            LastName = lastName;
            JobTitle = jobTitle;
            ExperienceLevel = experienceLevel;
            YearsOfExperience = yearsOfExperience;
            Skills = skills;
            Education = education;
            LinkedInProfile = linkedInProfile;
            ResumeUrl = resumeUrl;
            Address = address;
            City = city;
            State = state;
            Country = country;
            ZipCode = zipCode;
        }
    }
}
