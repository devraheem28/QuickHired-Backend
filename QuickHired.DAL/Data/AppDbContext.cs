using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuickHired.BOL;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickHired.DAL.Data
{
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }
        public AppDbContext() : base()
        {
        }
        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    base.OnConfiguring(optionsBuilder);
        //    optionsBuilder.UseSqlServer(@"Server=DELLPRO3530;Database=QuickHired;Trusted_Connection=True");
        //}

        /// <summary>
        /// Configures the model for the database context.
        /// </summary>
        /// <param name="modelBuilder">The builder used to construct the model for the context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (modelBuilder == null)
                throw new ArgumentNullException("modelBuilder");

            // Configure table names and delete behavior for relationships
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.SetTableName(entityType.DisplayName());

                entityType.GetForeignKeys()
                    .Where(fk => !fk.IsOwnership && fk.DeleteBehavior == DeleteBehavior.Cascade)
                    .ToList()
                    .ForEach(fk => fk.DeleteBehavior = DeleteBehavior.Restrict);
            }
            base.OnModelCreating(modelBuilder);
        }

        // Db Sets for entity types
        public DbSet<AppUsers> AppUsers { get; set; }
        public DbSet<Meetings> Meetings { get; set; }
        public DbSet<UserSubscriptions> UserSubscriptions { get; set; }
        public DbSet<VerificationCodes> VerificationCodes { get; set; }
        public DbSet<InterviewQuestions> InterviewQuestions { get; set; }
        public DbSet<PasswordResetToken> PasswordResetToken { get; set; }
        public DbSet<Persona> Personas { get; set; }
        public DbSet<InterviewReport> InterviewReports { get; set; }
        public DbSet<MeetingRecord> MeetingRecords { get; set; }
        public DbSet<UserThemePreference> UserThemePreferences { get; set; }
        public DbSet<Subscriptions> Subscriptions { get; set; }
        public DbSet<PromoCodes> PromoCodes { get; set; }
        public DbSet<Resume> Resumes { get; set; }
        public DbSet<Experience> Experiences { get; set; }
        public DbSet<Education> Educations { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Certification> Certifications { get; set; }
        public DbSet<LanguageItem> Languages { get; set; }
        public DbSet<SkillItem> Skills { get; set; }

    }
}
