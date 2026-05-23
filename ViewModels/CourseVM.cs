using System.ComponentModel.DataAnnotations;

namespace LMS.ViewModels;

public class CourseVM
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [StringLength(280)]
    public string ShortDescription { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    [RegularExpression("^(Beginner|Intermediate|Advanced)$", ErrorMessage = "Select a valid course level.")]
    public string Level { get; set; } = "Beginner";

    [Range(1, int.MaxValue)]
    public int DurationWeeks { get; set; } = 12;

    [Range(1, int.MaxValue)]
    public int Capacity { get; set; } = 30;

    [RegularExpression("^(Open|Approval)$", ErrorMessage = "Select a valid enrollment type.")]
    public string EnrollmentType { get; set; } = "Open";

    public bool AllowDiscussions { get; set; } = true;

    [RegularExpression("^(Draft|Published|Archived)$", ErrorMessage = "Select a valid course status.")]
    public string Status { get; set; } = "Draft";
}

public class InstructorCoursesVM
{
    public List<InstructorCourseRowVM> Courses { get; set; } = new();
}

public class InstructorCourseRowVM
{
    public LMS.Models.Course Course { get; set; } = new();
    public int ApprovedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int Assignments { get; set; }
    public int Quizzes { get; set; }
}

public class CourseIndexVM
{
    public bool IsManagementView { get; set; }
    public bool IsMyCoursesPage { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public List<CourseCardVM> Courses { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public HashSet<int> EnrolledCourseIds { get; set; } = new();
}

public class CourseCardVM
{
    public LMS.Models.Course Course { get; set; } = new();
    public int ApprovedEnrollmentCount { get; set; }
    public int PendingEnrollmentCount { get; set; }
    public int LectureCount { get; set; }
    public int QuizCount { get; set; }
    public double CourseRating { get; set; }
    public double InstructorRating { get; set; }
    public int CourseReviewCount { get; set; }
    public int InstructorReviewCount { get; set; }
    public bool IsAtCapacity => Course.Capacity > 0 && ApprovedEnrollmentCount >= Course.Capacity;
}

public class CourseDetailsVM : CourseCardVM
{
    public string? UserId { get; set; }
    public bool IsCourseStaff { get; set; }
    public bool IsStudent { get; set; }
    public LMS.Models.Enrollment? Enrollment { get; set; }
    public LMS.Models.CourseReview? MyReview { get; set; }
    public List<LMS.Models.CourseReview> RecentReviews { get; set; } = new();
    public List<LMS.Models.Course> RelatedCourses { get; set; } = new();
    public int ResourceCount { get; set; }
}

public class CourseContentVM
{
    public LMS.Models.Course Course { get; set; } = new();
    public List<LMS.Models.Module> Modules { get; set; } = new();
    public LMS.Models.ContentItem? SelectedResource { get; set; }
    public HashSet<int> CompletedResourceIds { get; set; } = new();
    public bool IsStudent { get; set; }
    public int TotalResources { get; set; }
    public int VideoResources { get; set; }
    public double ProgressPercent { get; set; }
    public bool CertificateAvailable { get; set; }
}

public class CourseDiscussionVM
{
    public LMS.Models.Course Course { get; set; } = new();
    public bool CanPost { get; set; }
    public List<LMS.Models.CourseDiscussionPost> Posts { get; set; } = new();
}

public class CourseCertificateVM
{
    public LMS.Models.Course Course { get; set; } = new();
    public string StudentName { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; }
    public string CertificateCode { get; set; } = string.Empty;
}
