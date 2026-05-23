namespace LMS.ViewModels;

public class StudentProgressVM
{
    public List<StudentProgressCourseVM> Courses { get; set; } = new();
    public int SelectedCourseId { get; set; }
}

public class StudentProgressCourseVM
{
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Total { get; set; }
    public double Percent { get; set; }
    public int AssignmentCount { get; set; }
    public int QuizCount { get; set; }
    public int GradedAssignments { get; set; }
    public double AssignmentAverage { get; set; }
    public int QuizAttempts { get; set; }
    public double QuizAverage { get; set; }
}

public class InstructorCourseStudentsVM
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int TotalResources { get; set; }
    public List<InstructorStudentRowVM> Students { get; set; } = new();
}

public class InstructorStudentRowVM
{
    public string StudentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Total { get; set; }
    public double Percent { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class InstructorStudentProgressVM
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Total { get; set; }
    public double Percent { get; set; }
    public List<InstructorResourceProgressVM> Resources { get; set; } = new();
}

public class InstructorResourceProgressVM
{
    public string ModuleTitle { get; set; } = string.Empty;
    public int ContentItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}
