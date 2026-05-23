namespace LMS.Models;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner";
    public int DurationWeeks { get; set; } = 12;
    public string InstructorId { get; set; } = string.Empty;
    public ApplicationUser? Instructor { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Published, Archived
    public string EnrollmentType { get; set; } = "Open"; // Open, Approval
    public int Capacity { get; set; } = 30;
    public bool AllowDiscussions { get; set; } = true;
    public bool IsEnded { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<CourseReview> Reviews { get; set; } = new List<CourseReview>();
    public ICollection<CourseDiscussionPost> DiscussionPosts { get; set; } = new List<CourseDiscussionPost>();

    public IEnumerable<string> TagList => Tags
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
