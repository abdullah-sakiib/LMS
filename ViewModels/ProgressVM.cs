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
