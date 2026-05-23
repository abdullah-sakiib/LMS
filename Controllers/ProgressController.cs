using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize(Roles = "Student")]
[AutoValidateAntiforgeryToken]
public class ProgressController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProgressController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // POST: Mark a content item complete/incomplete
    [HttpPost]
    public async Task<IActionResult> Toggle(int contentItemId, int courseId)
    {
        var userId = _userManager.GetUserId(User)!;
        if (!await CanTrackProgressAsync(userId, contentItemId, courseId)) return Forbid();

        var progress = await _db.StudentProgress
            .FirstOrDefaultAsync(p => p.StudentId == userId && p.ContentItemId == contentItemId);

        if (progress == null)
        {
            _db.StudentProgress.Add(new StudentProgress
            {
                StudentId = userId,
                CourseId = courseId,
                ContentItemId = contentItemId,
                IsCompleted = true,
                CompletedAt = DateTime.UtcNow
            });
        }
        else
        {
            progress.IsCompleted = !progress.IsCompleted;
            progress.CompletedAt = progress.IsCompleted ? DateTime.UtcNow : null;
        }

        await _db.SaveChangesAsync();

        // Return updated completion % for this course
        var totalItems = await _db.ContentItems
            .Where(ci => ci.Module != null && ci.Module.CourseId == courseId)
            .CountAsync();

        var completedItems = await _db.StudentProgress
            .Where(p => p.StudentId == userId && p.CourseId == courseId && p.IsCompleted)
            .CountAsync();

        double percent = totalItems > 0 ? Math.Round((double)completedItems / totalItems * 100, 1) : 0;

        return Json(new { isCompleted = progress?.IsCompleted ?? true, percent, completedItems, totalItems });
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus(int contentItemId, int courseId)
    {
        var userId = _userManager.GetUserId(User)!;
        if (!await CanTrackProgressAsync(userId, contentItemId, courseId)) return Forbid();

        var progress = await _db.StudentProgress
            .FirstOrDefaultAsync(p => p.StudentId == userId && p.ContentItemId == contentItemId);

        var totalItems = await _db.ContentItems
            .Where(ci => ci.Module != null && ci.Module.CourseId == courseId)
            .CountAsync();

        var completedItems = await _db.StudentProgress
            .Where(p => p.StudentId == userId && p.CourseId == courseId && p.IsCompleted)
            .CountAsync();

        double percent = totalItems > 0 ? Math.Round((double)completedItems / totalItems * 100, 1) : 0;

        return Json(new
        {
            isCompleted = progress?.IsCompleted ?? false,
            percent,
            completedItems,
            totalItems
        });
    }

    // GET: My progress across all enrolled courses
    public async Task<IActionResult> Index(int? courseId)
    {
        var userId = _userManager.GetUserId(User)!;

        var enrolledCourseIds = await _db.Enrollments
            .Where(e => e.StudentId == userId && e.Status == "Approved")
            .Select(e => e.CourseId)
            .ToListAsync();

        var courses = await _db.Courses
            .Include(c => c.Modules).ThenInclude(m => m.ContentItems)
            .Where(c => enrolledCourseIds.Contains(c.Id) && c.Status == "Published")
            .ToListAsync();
        var visibleCourseIds = courses.Select(c => c.Id).ToList();

        var progressRecords = await _db.StudentProgress
            .Where(p => p.StudentId == userId && p.IsCompleted)
            .ToListAsync();

        var assignmentCounts = await _db.Assignments
            .Where(item => visibleCourseIds.Contains(item.CourseId))
            .GroupBy(item => item.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToListAsync();

        var quizCounts = await _db.Quizzes
            .Where(item => visibleCourseIds.Contains(item.CourseId) && item.IsPublished)
            .GroupBy(item => item.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToListAsync();

        var submissions = await _db.AssignmentSubmissions
            .Include(item => item.Assignment)
            .Where(item => item.StudentId == userId && item.Assignment != null && visibleCourseIds.Contains(item.Assignment.CourseId))
            .ToListAsync();

        var quizAttempts = await _db.QuizAttempts
            .Include(item => item.Quiz)
            .Where(item => item.StudentId == userId && item.SubmittedAt != null && item.Quiz != null && visibleCourseIds.Contains(item.Quiz.CourseId))
            .ToListAsync();

        var completedIds = progressRecords.Select(p => p.ContentItemId).ToHashSet();

        var result = courses.Select(c =>
        {
            var allItems = c.Modules.SelectMany(m => m.ContentItems).ToList();
            var completed = allItems.Count(ci => completedIds.Contains(ci.Id));
            var total = allItems.Count;
            var courseSubmissions = submissions.Where(item => item.Assignment!.CourseId == c.Id).ToList();
            var graded = courseSubmissions.Where(item => item.Grade.HasValue).ToList();
            var assignmentAverage = graded.Count == 0
                ? 0
                : Math.Round(graded.Average(item => item.Grade!.Value / Math.Max(1, item.Assignment!.MaxScore) * 100), 1);
            var courseQuizAttempts = quizAttempts.Where(item => item.Quiz!.CourseId == c.Id).ToList();
            var quizAverage = courseQuizAttempts.Count == 0
                ? 0
                : Math.Round(courseQuizAttempts.Average(item => item.Score / Math.Max(1, item.Quiz!.TotalMarks) * 100), 1);
            return new
            {
                Course = c,
                Completed = completed,
                Total = total,
                Percent = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0.0,
                AssignmentCount = assignmentCounts.FirstOrDefault(item => item.CourseId == c.Id)?.Count ?? 0,
                QuizCount = quizCounts.FirstOrDefault(item => item.CourseId == c.Id)?.Count ?? 0,
                GradedAssignments = graded.Count,
                AssignmentAverage = assignmentAverage,
                QuizAttempts = courseQuizAttempts.Count,
                QuizAverage = quizAverage
            };
        }).ToList();
        var courseCards = result.Select(item => new StudentProgressCourseVM
        {
            CourseId = item.Course.Id,
            Title = item.Course.Title,
            Completed = item.Completed,
            Total = item.Total,
            Percent = item.Percent,
            AssignmentCount = item.AssignmentCount,
            QuizCount = item.QuizCount,
            GradedAssignments = item.GradedAssignments,
            AssignmentAverage = item.AssignmentAverage,
            QuizAttempts = item.QuizAttempts,
            QuizAverage = item.QuizAverage
        }).OrderBy(item => item.Title).ToList();

        var selectedCourseId = courseId ?? courseCards.FirstOrDefault()?.CourseId ?? 0;
        return View(new StudentProgressVM
        {
            Courses = courseCards,
            SelectedCourseId = selectedCourseId
        });
    }

    private async Task<bool> CanTrackProgressAsync(string userId, int contentItemId, int courseId)
    {
        var hasContent = await _db.ContentItems.AnyAsync(item => item.Id == contentItemId &&
            item.Module != null && item.Module.CourseId == courseId &&
            item.Module.Course != null && item.Module.Course.Status == "Published");
        return hasContent && await _db.Enrollments.AnyAsync(enrollment => enrollment.StudentId == userId &&
            enrollment.CourseId == courseId && enrollment.Status == "Approved");
    }
}
