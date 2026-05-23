using System.ComponentModel.DataAnnotations;

namespace LMS.ViewModels;

public class AssignmentVM
{
    public int Id { get; set; }
    public int CourseId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime Deadline { get; set; } = DateTime.Today.AddDays(7);

    [Range(1, int.MaxValue)]
    public int MaxScore { get; set; } = 100;
}

public class AssignmentWorkspaceVM
{
    public bool IsStudent { get; set; }
    public List<AssignmentRowVM> Rows { get; set; } = new();
}

public class AssignmentRowVM
{
    public LMS.Models.Assignment Assignment { get; set; } = new();
    public LMS.Models.AssignmentSubmission? Submission { get; set; }
}
