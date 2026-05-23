namespace LMS.Models;

public class Grade
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser? Student { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public int? AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }
    public int? QuizAttemptId { get; set; }
    public QuizAttempt? QuizAttempt { get; set; }
    public double Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;
}