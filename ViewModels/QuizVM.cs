namespace LMS.ViewModels;

public class QuizVM
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalMarks { get; set; } = 100;
    public int TimerMinutes { get; set; } = 30;
    public List<QuizQuestionVM> Questions { get; set; } = new();
}

public class QuizQuestionVM
{
    public string QuestionText { get; set; } = string.Empty;
    public string Type { get; set; } = "MCQ"; // MCQ or TrueFalse
    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }
}

public class QuizSubmitVM
{
    public int QuizId { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new(); // questionId -> optionId
}

public class QuizWorkspaceVM
{
    public bool IsStudent { get; set; }
    public List<LMS.Models.Course> InstructorCourses { get; set; } = new();
    public List<QuizCardVM> Cards { get; set; } = new();
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Pending { get; set; }
    public double AveragePercent { get; set; }
    public int TotalAttempts { get; set; }
    public int TotalStudents { get; set; }
    public double ParticipationPercent { get; set; }
}

public class QuizCardVM
{
    public LMS.Models.Quiz Quiz { get; set; } = new();
    public LMS.Models.QuizAttempt? MyAttempt { get; set; }
    public int QuestionCount { get; set; }
    public int EligibleStudents { get; set; }
    public int AttemptCount { get; set; }
    public double AverageScorePercent { get; set; }
    public double ParticipationPercent { get; set; }
    public List<LMS.Models.ApplicationUser> AttemptedStudents { get; set; } = new();
    public List<LMS.Models.ApplicationUser> NotAttemptedStudents { get; set; } = new();
}

public class QuizTakeVM
{
    public LMS.Models.Quiz Quiz { get; set; } = new();
    public LMS.Models.QuizAttempt? Attempt { get; set; }
    public bool IsSubmitted => Attempt?.SubmittedAt != null;
    public Dictionary<int, LMS.Models.QuizResponse> Responses { get; set; } = new();
}
