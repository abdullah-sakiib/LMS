namespace LMS.Models;

public class Enrollment
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser? Student { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Dropped
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
}
