using System.ComponentModel.DataAnnotations;

namespace LMS.Models;

public class CourseReview
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser? Student { get; set; }

    [Range(1, 5)]
    public int CourseRating { get; set; }

    [Range(1, 5)]
    public int InstructorRating { get; set; }

    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
