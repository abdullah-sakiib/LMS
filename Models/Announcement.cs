namespace LMS.Models;

public class Announcement
{
    public int Id { get; set; }
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
    public string? InstructorId { get; set; }
    public ApplicationUser? Instructor { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsGlobal => !CourseId.HasValue;
}
