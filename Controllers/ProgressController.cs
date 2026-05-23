using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize(Roles = "Student")]
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
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;

        var enrolledCourseIds = await _db.Enrollments
            .Where(e => e.StudentId == userId && e.Status == "Approved")
            .Select(e => e.CourseId)
            .ToListAsync();

        var courses = await _db.Courses
            .Include(c => c.Modules).ThenInclude(m => m.ContentItems)
            .Where(c => enrolledCourseIds.Contains(c.Id))
            .ToListAsync();

        var progressRecords = await _db.StudentProgress
            .Where(p => p.StudentId == userId && p.IsCompleted)
            .ToListAsync();

        var completedIds = progressRecords.Select(p => p.ContentItemId).ToHashSet();

        var result = courses.Select(c =>
        {
            var allItems = c.Modules.SelectMany(m => m.ContentItems).ToList();
            var completed = allItems.Count(ci => completedIds.Contains(ci.Id));
            var total = allItems.Count;
            return new
            {
                Course = c,
                Completed = completed,
                Total = total,
                Percent = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0.0
            };
        }).ToList();

        ViewBag.ProgressData = result;
        return View();
    }

    private async Task<bool> CanTrackProgressAsync(string userId, int contentItemId, int courseId)
    {
        var hasContent = await _db.ContentItems.AnyAsync(item => item.Id == contentItemId &&
            item.Module != null && item.Module.CourseId == courseId);
        return hasContent && await _db.Enrollments.AnyAsync(enrollment => enrollment.StudentId == userId &&
            enrollment.CourseId == courseId && enrollment.Status == "Approved");
    }
}
