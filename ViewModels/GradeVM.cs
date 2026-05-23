namespace LMS.ViewModels;

public class GradeSubmitVM
{
    public int SubmissionId { get; set; }
    public double Score { get; set; }
    public string? Feedback { get; set; }
    public bool ReturnToWorkspace { get; set; }
}

public class InstructorSubmissionsVM
{
    public List<LMS.Models.AssignmentSubmission> Submissions { get; set; } = new();
    public List<string> CourseTitles { get; set; } = new();
    public int Pending { get; set; }
    public int Graded { get; set; }
    public int Late { get; set; }
}

public class StudentGradesVM
{
    public List<LMS.Models.AssignmentSubmission> Submissions { get; set; } = new();
    public List<LMS.Models.QuizAttempt> QuizAttempts { get; set; } = new();
    public double AssignmentAverage { get; set; }
    public double QuizAverage { get; set; }
    public int GradedAssignments { get; set; }
}
