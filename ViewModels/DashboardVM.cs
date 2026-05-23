namespace LMS.ViewModels;

public class DashboardVM
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public int EnrolledCourses { get; set; }
    public int PendingAssignments { get; set; }
    public int PendingQuizzes { get; set; }
    public double AssignmentMarks { get; set; }
    public double QuizMarks { get; set; }
    public double Completion { get; set; }

    public int TotalCourses { get; set; }
    public int TotalStudents { get; set; }
    public int PendingSubmissions { get; set; }

    public List<DashboardCourseVM> Courses { get; set; } = new();
    public List<DashboardCourseVM> PopularCourses { get; set; } = new();
    public List<TopInstructorVM> TopInstructors { get; set; } = new();
    public List<PendingSubmissionVM> PendingReviews { get; set; } = new();
    public List<InstructorStudentVM> TopStudents { get; set; } = new();
}

public class DashboardCourseVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EnrollmentCount { get; set; }
    public int Progress { get; set; }
    public int AssignmentTotal { get; set; }
    public int AssignmentSubmitted { get; set; }
    public int AssignmentPending { get; set; }
    public double AssignmentAverage { get; set; }
    public int QuizTotal { get; set; }
    public int QuizCompleted { get; set; }
    public int QuizPending { get; set; }
    public double QuizAverage { get; set; }
}

public class PendingSubmissionVM
{
    public string AssignmentTitle { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
}

public class TopInstructorVM
{
    public string Name { get; set; } = string.Empty;
    public int Courses { get; set; }
    public int Students { get; set; }
}

public class InstructorStudentVM
{
    public string Name { get; set; } = string.Empty;
    public int Courses { get; set; }
}

public class AdminDashboardVM
{
    public string FullName { get; set; } = string.Empty;
    public int TotalUsers { get; set; }
    public int TotalCourses { get; set; }
    public int TotalEnrollments { get; set; }
    public int TotalSubmissions { get; set; }
    public int TotalQuizAttempts { get; set; }
    public int Students { get; set; }
    public int Instructors { get; set; }
    public List<int> RegistrationsByDay { get; set; } = new();
    public List<AdminUserRowVM> RecentUsers { get; set; } = new();
    public List<AdminCourseRowVM> Courses { get; set; } = new();
}

public class AdminUserRowVM
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class AdminCourseRowVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public int Students { get; set; }
    public string Status { get; set; } = string.Empty;
}
