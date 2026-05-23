namespace LMS.Models;

public class Assignment
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxScore { get; set; } = 100;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}
