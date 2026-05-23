using System.ComponentModel.DataAnnotations;
using LMS.Models;

namespace LMS.ViewModels;

public class PasswordResetVM
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ProfileVM
{
    public ApplicationUser Profile { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    public bool IsStudent => Role == "Student";
    public string MemberCode { get; set; } = string.Empty;
    public int CourseCount { get; set; }
    public int Submitted { get; set; }
    public double AverageGrade { get; set; }
    public int Certificates { get; set; }
    public int TotalStudents { get; set; }
    public double AverageRating { get; set; }
    public int RatedCourses { get; set; }
    public List<ProfileCourseVM> Courses { get; set; } = new();
    public List<ProfileGradeVM> GradeSummary { get; set; } = new();
}

public class ProfileCourseVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public double Progress { get; set; }
    public bool IsCourseEnded { get; set; }
    public bool CertificateAvailable { get; set; }
    public int StudentCount { get; set; }
    public double Rating { get; set; }
}

public class ProfileGradeVM
{
    public string Course { get; set; } = string.Empty;
    public double AveragePercent { get; set; }
}

public class AnnouncementFeedVM
{
    public List<Announcement> Announcements { get; set; } = new();
    public List<Course> PostingCourses { get; set; } = new();
    public bool CanPost { get; set; }
}
