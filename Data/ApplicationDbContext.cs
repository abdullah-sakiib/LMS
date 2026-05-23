using LMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Course> Courses { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<QuizQuestion> QuizQuestions { get; set; }
    public DbSet<QuizOption> QuizOptions { get; set; }
    public DbSet<QuizAttempt> QuizAttempts { get; set; }
    public DbSet<QuizResponse> QuizResponses { get; set; }
    public DbSet<StudentProgress> StudentProgress { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<CourseReview> CourseReviews { get; set; }
    public DbSet<CourseDiscussionPost> CourseDiscussionPosts { get; set; }

    public DbSet<Module> Modules { get; set; }
    public DbSet<ContentItem> ContentItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CourseReview>()
            .HasIndex(review => new { review.CourseId, review.StudentId })
            .IsUnique();

        builder.Entity<Enrollment>()
            .HasIndex(enrollment => new { enrollment.CourseId, enrollment.StudentId })
            .IsUnique();

        builder.Entity<AssignmentSubmission>()
            .HasIndex(submission => new { submission.AssignmentId, submission.StudentId })
            .IsUnique();

        builder.Entity<QuizAttempt>()
            .HasIndex(attempt => new { attempt.QuizId, attempt.StudentId })
            .IsUnique();

        builder.Entity<QuizResponse>()
            .HasIndex(response => new { response.QuizAttemptId, response.QuizQuestionId })
            .IsUnique();

        builder.Entity<Announcement>()
            .HasOne(announcement => announcement.Course)
            .WithMany()
            .HasForeignKey(announcement => announcement.CourseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Announcement>()
            .HasOne(announcement => announcement.Instructor)
            .WithMany()
            .HasForeignKey(announcement => announcement.InstructorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
