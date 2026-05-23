using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class AnnouncementController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnnouncementController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int courseId)
    {
        var course = await _db.Courses.FirstOrDefaultAsync(item => item.Id == courseId);
        if (course == null) return NotFound();

        var userId = _userManager.GetUserId(User)!;
        if (!await CanViewCourseAsync(course, userId)) return Forbid();

        var announcements = await _db.Announcements
            .Include(item => item.Instructor)
            .Where(item => item.CourseId == courseId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();

        ViewBag.Course = course;
        ViewBag.IsInstructor = CanManageCourse(course, userId);
        return View(announcements);
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create(int courseId)
    {
        var course = await _db.Courses.FindAsync(courseId);
        if (course == null) return NotFound();
        if (!CanManageCourse(course, _userManager.GetUserId(User)!)) return Forbid();

        ViewBag.CourseId = courseId;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create(int courseId, string title, string body)
    {
        var course = await _db.Courses.FindAsync(courseId);
        if (course == null) return NotFound();
        if (!CanManageCourse(course, _userManager.GetUserId(User)!)) return Forbid();

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            ViewBag.CourseId = courseId;
            ModelState.AddModelError("", "Title and body are required.");
            return View();
        }

        _db.Announcements.Add(new Announcement
        {
            CourseId = courseId,
            InstructorId = _userManager.GetUserId(User)!,
            Title = title.Trim(),
            Body = body.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Announcement posted successfully.";
        return RedirectToAction(nameof(Index), new { courseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var announcement = await _db.Announcements
            .Include(item => item.Course)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (announcement == null) return NotFound();

        var userId = _userManager.GetUserId(User)!;
        var ownsPost = announcement.InstructorId == userId;
        var ownsCourse = announcement.Course == null || announcement.Course.InstructorId == userId;
        if (!User.IsInRole("Admin") && (!ownsPost || !ownsCourse)) return Forbid();

        var courseId = announcement.CourseId;
        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync();
        return courseId.HasValue
            ? RedirectToAction(nameof(Index), new { courseId })
            : RedirectToAction(nameof(MyFeed));
    }

    public async Task<IActionResult> MyFeed()
    {
        var userId = _userManager.GetUserId(User)!;
        var canPost = User.IsInRole("Instructor") || User.IsInRole("Admin");
        IQueryable<Announcement> query = _db.Announcements
            .Include(item => item.Course)
            .Include(item => item.Instructor);

        if (User.IsInRole("Student"))
        {
            var enrolledCourseIds = await _db.Enrollments
                .Where(item => item.StudentId == userId && item.Status == "Approved")
                .Select(item => item.CourseId)
                .ToListAsync();
            query = query.Where(item => item.CourseId == null || enrolledCourseIds.Contains(item.CourseId.Value));
        }
        else if (User.IsInRole("Instructor"))
        {
            query = query.Where(item => item.CourseId == null || item.Course!.InstructorId == userId);
        }

        var vm = new AnnouncementFeedVM
        {
            CanPost = canPost,
            Announcements = await query.OrderByDescending(item => item.CreatedAt).ToListAsync()
        };

        if (canPost)
        {
            vm.PostingCourses = await _db.Courses
                .Where(item => User.IsInRole("Admin") || item.InstructorId == userId)
                .OrderBy(item => item.Title)
                .ToListAsync();
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Post(string title, string body, int? courseId)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            TempData["Error"] = "Title and body are required.";
            return RedirectToAction(nameof(MyFeed));
        }

        var userId = _userManager.GetUserId(User)!;
        if (courseId.HasValue)
        {
            var course = await _db.Courses.FindAsync(courseId.Value);
            if (course == null || !CanManageCourse(course, userId))
            {
                TempData["Error"] = "Please select a valid course for this announcement.";
                return RedirectToAction(nameof(MyFeed));
            }
        }

        _db.Announcements.Add(new Announcement
        {
            CourseId = courseId,
            InstructorId = userId,
            Title = title.Trim(),
            Body = body.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Announcement posted successfully.";
        return RedirectToAction(nameof(MyFeed));
    }

    private bool CanManageCourse(Course course, string userId) =>
        User.IsInRole("Admin") || course.InstructorId == userId;

    private async Task<bool> CanViewCourseAsync(Course course, string userId)
    {
        if (CanManageCourse(course, userId)) return true;
        return await _db.Enrollments.AnyAsync(item =>
            item.CourseId == course.Id && item.StudentId == userId && item.Status == "Approved");
    }
}
