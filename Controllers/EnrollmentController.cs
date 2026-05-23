using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class EnrollmentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public EnrollmentController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Student enrolls in a course
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Enroll(int courseId)
    {
        var user = await _userManager.GetUserAsync(User);

        var already = await _db.Enrollments
            .AnyAsync(e => e.StudentId == user!.Id && e.CourseId == courseId);

        if (already)
        {
            TempData["Error"] = "You are already enrolled or pending approval.";
            return RedirectToAction("Details", "Course", new { id = courseId });
        }

        var course = await _db.Courses
            .Include(item => item.Enrollments)
            .FirstOrDefaultAsync(item => item.Id == courseId && item.Status == "Published");
        if (course == null) return NotFound();
        if (course.IsEnded)
        {
            TempData["Error"] = "This course has ended. New enrollments are closed.";
            return RedirectToAction("Details", "Course", new { id = courseId });
        }
        if (course.Capacity > 0 && course.Enrollments.Count(item => item.Status == "Approved") >= course.Capacity)
        {
            TempData["Error"] = $"{course.Title} has reached maximum student capacity.";
            return RedirectToAction("Details", "Course", new { id = courseId });
        }

        var enrollment = new Enrollment
        {
            StudentId = user!.Id,
            CourseId = courseId,
            Status = course.EnrollmentType == "Approval" ? "Pending" : "Approved",
            ApprovedAt = course.EnrollmentType == "Approval" ? null : DateTime.UtcNow
        };

        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        TempData["Success"] = course.EnrollmentType == "Approval"
            ? "Enrollment request sent. Awaiting approval."
            : $"You are now enrolled in {course.Title}.";
        return RedirectToAction("Details", "Course", new { id = courseId });
    }

    // Student drops a course
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Drop(int courseId)
    {
        var user = await _userManager.GetUserAsync(User);

        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == user!.Id && e.CourseId == courseId);

        if (enrollment != null)
        {
            _db.Enrollments.Remove(enrollment);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "You have dropped the course.";
        return RedirectToAction("Index", "Course");
    }

    // Instructor views pending enrollments for their courses
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Requests()
    {
        var user = await _userManager.GetUserAsync(User);

        var enrollmentsQuery = _db.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Where(e => e.Status == "Pending");
        if (!User.IsInRole("Admin"))
            enrollmentsQuery = enrollmentsQuery.Where(e => e.Course!.InstructorId == user!.Id);
        var enrollments = await enrollmentsQuery.ToListAsync();

        ViewData["Title"] = "Enrollment Requests";
        return View(enrollments);
    }

    // Instructor approves enrollment
    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Approve(int id, bool returnToCourses = false)
    {
        var enrollment = await _db.Enrollments.FindAsync(id);
        if (enrollment != null && await CanManageCourseAsync(enrollment.CourseId))
        {
            enrollment.Status = "Approved";
            enrollment.ApprovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return returnToCourses ? RedirectToAction("Index", "Course") : RedirectToAction(nameof(Requests));
    }

    // Instructor rejects enrollment
    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Reject(int id, bool returnToCourses = false)
    {
        var enrollment = await _db.Enrollments.FindAsync(id);
        if (enrollment != null && await CanManageCourseAsync(enrollment.CourseId))
        {
            _db.Enrollments.Remove(enrollment);
            await _db.SaveChangesAsync();
        }
        return returnToCourses ? RedirectToAction("Index", "Course") : RedirectToAction(nameof(Requests));
    }

    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Remove(int id)
    {
        var enrollment = await _db.Enrollments.FindAsync(id);
        if (enrollment != null && await CanManageCourseAsync(enrollment.CourseId))
        {
            _db.Enrollments.Remove(enrollment);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Student removed from the course.";
        }
        return RedirectToAction("Index", "Course");
    }

    private async Task<bool> CanManageCourseAsync(int courseId)
    {
        if (User.IsInRole("Admin")) return await _db.Courses.AnyAsync(c => c.Id == courseId);
        var userId = _userManager.GetUserId(User);
        return await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == userId);
    }
}
