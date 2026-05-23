namespace LMS.Models;

public class StudentProgress
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser? Student { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public int ContentItemId { get; set; }
    public ContentItem? ContentItem { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}