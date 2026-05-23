using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class GradeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public GradeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Grade book — all students, all grades for a course
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Book(int courseId)
    {
        if (!await CanManageCourseAsync(courseId)) return NotFound();
        var course = await _db.Courses
            .Include(c => c.Enrollments).ThenInclude(e => e.Student)
            .Include(c => c.Assignments)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null) return NotFound();

        var submissions = await _db.AssignmentSubmissions
            .Include(s => s.Assignment)
            .Include(s => s.Student)
            .Where(s => s.Assignment != null && s.Assignment.CourseId == courseId)
            .ToListAsync();

        var quizAttempts = await _db.QuizAttempts
            .Include(a => a.Quiz)
            .Include(a => a.Student)
            .Where(a => a.Quiz != null && a.Quiz.CourseId == courseId && a.SubmittedAt != null)
            .ToListAsync();

        ViewBag.Course = course;
        ViewBag.Submissions = submissions;
        ViewBag.QuizAttempts = quizAttempts;
        return View();
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Submissions()
    {
        var userId = _userManager.GetUserId(User);
        var submissionsQuery = _db.AssignmentSubmissions
            .Include(s => s.Assignment).ThenInclude(a => a!.Course)
            .Include(s => s.Student)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
            submissionsQuery = submissionsQuery.Where(s => s.Assignment!.Course!.InstructorId == userId);

        var submissions = await submissionsQuery.OrderByDescending(s => s.SubmittedAt).ToListAsync();
        var vm = new InstructorSubmissionsVM
        {
            Submissions = submissions,
            CourseTitles = submissions.Select(s => s.Assignment!.Course!.Title).Distinct().OrderBy(t => t).ToList(),
            Pending = submissions.Count(s => !s.Grade.HasValue),
            Graded = submissions.Count(s => s.Grade.HasValue),
            Late = submissions.Count(s => s.Status == "Late")
        };

        ViewData["Title"] = "Submissions";
        ViewData["PageTitle"] = "Submissions";
        ViewData["PageSubtitle"] = "Review and grade student work";
        return View(vm);
    }

    // POST: Grade a submission
    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> GradeSubmission(GradeSubmitVM vm)
    {
        var submission = await _db.AssignmentSubmissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.Id == vm.SubmissionId);

        if (submission == null) return NotFound();
        if (!await CanManageCourseAsync(submission.Assignment!.CourseId)) return NotFound();

        submission.Grade = Math.Clamp(vm.Score, 0, submission.Assignment.MaxScore);
        submission.Feedback = vm.Feedback;
        submission.Status = "Graded";
        submission.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Grade saved.";
        return vm.ReturnToWorkspace
            ? RedirectToAction(nameof(Submissions))
            : RedirectToAction("Book", new { courseId = submission.Assignment.CourseId });
    }

    // Student: My grades across all courses
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> MyGrades()
    {
        var userId = _userManager.GetUserId(User)!;

        var submissions = await _db.AssignmentSubmissions
            .Include(s => s.Assignment).ThenInclude(a => a!.Course)
            .Where(s => s.StudentId == userId)
            .ToListAsync();

        var quizAttempts = await _db.QuizAttempts
            .Include(a => a.Quiz).ThenInclude(q => q!.Course)
            .Where(a => a.StudentId == userId && a.SubmittedAt != null)
            .ToListAsync();

        var graded = submissions.Where(item => item.Grade.HasValue).ToList();
        ViewData["Title"] = "My Grades";
        ViewData["PageTitle"] = "My Grades";
        ViewData["PageSubtitle"] = "Real-time assignment and quiz performance";
        return View(new StudentGradesVM
        {
            Submissions = submissions.OrderByDescending(item => item.SubmittedAt).ToList(),
            QuizAttempts = quizAttempts.OrderByDescending(item => item.SubmittedAt).ToList(),
            AssignmentAverage = graded.Count == 0 ? 0 : Math.Round(graded.Average(item =>
                item.Grade!.Value / Math.Max(1, item.Assignment!.MaxScore) * 100), 2),
            QuizAverage = quizAttempts.Count == 0 ? 0 : Math.Round(quizAttempts.Average(item =>
                item.Score / Math.Max(1, item.Quiz!.TotalMarks) * 100), 2),
            GradedAssignments = graded.Count
        });
    }

    private async Task<bool> CanManageCourseAsync(int courseId)
    {
        if (User.IsInRole("Admin")) return await _db.Courses.AnyAsync(c => c.Id == courseId);
        var userId = _userManager.GetUserId(User);
        return await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == userId);
    }
}
