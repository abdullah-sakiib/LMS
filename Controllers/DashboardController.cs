using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public DashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);
        var role = roles.FirstOrDefault() ?? "Student";

        if (role == "Admin")
            return RedirectToAction("Index", "Admin");

        var vm = new DashboardVM
        {
            FullName = user!.FullName,
            Role = role
        };

        if (role == "Student")
        {
            var enrolledCourseIds = await _db.Enrollments
                .Where(e => e.StudentId == user.Id && e.Status == "Approved")
                .Select(e => e.CourseId)
                .ToListAsync();
            var courses = await _db.Courses
                .Where(c => enrolledCourseIds.Contains(c.Id) && c.Status == "Published")
                .Include(c => c.Instructor)
                .Include(c => c.Modules).ThenInclude(m => m.ContentItems)
                .Include(c => c.Assignments)
                .ToListAsync();
            var courseIds = courses.Select(c => c.Id).ToList();
            var submissions = await _db.AssignmentSubmissions
                .Where(s => s.StudentId == user.Id && courseIds.Contains(s.Assignment!.CourseId))
                .Include(s => s.Assignment)
                .ToListAsync();
            var quizzes = await _db.Quizzes.Where(q => courseIds.Contains(q.CourseId)).ToListAsync();
            var attempts = await _db.QuizAttempts
                .Where(a => a.StudentId == user.Id && a.SubmittedAt != null && courseIds.Contains(a.Quiz!.CourseId))
                .Include(a => a.Quiz)
                .ToListAsync();
            var completedIds = await _db.StudentProgress
                .Where(p => p.StudentId == user.Id && p.IsCompleted)
                .Select(p => p.ContentItemId)
                .ToListAsync();
            var completeSet = completedIds.ToHashSet();

            vm.Courses = courses.Select(course =>
            {
                var assignments = course.Assignments.ToList();
                var courseSubmissions = submissions.Where(s => s.Assignment?.CourseId == course.Id).ToList();
                var courseQuizzes = quizzes.Where(q => q.CourseId == course.Id).ToList();
                var courseAttempts = attempts.Where(a => a.Quiz?.CourseId == course.Id).ToList();
                var items = course.Modules.SelectMany(m => m.ContentItems).ToList();
                var done = items.Count(i => completeSet.Contains(i.Id));
                return new DashboardCourseVM
                {
                    Id = course.Id,
                    Title = course.Title,
                    Instructor = course.Instructor?.FullName ?? "Instructor",
                    Status = course.Status,
                    Progress = items.Count == 0 ? 0 : (int)Math.Round((double)done / items.Count * 100),
                    AssignmentTotal = assignments.Count,
                    AssignmentSubmitted = courseSubmissions.Count,
                    AssignmentPending = Math.Max(0, assignments.Count - courseSubmissions.Count),
                    AssignmentAverage = courseSubmissions.Where(s => s.Grade.HasValue).Select(s => s.Grade!.Value).DefaultIfEmpty(0).Average(),
                    QuizTotal = courseQuizzes.Count,
                    QuizCompleted = courseAttempts.Count,
                    QuizPending = Math.Max(0, courseQuizzes.Count - courseAttempts.Count),
                    QuizAverage = courseAttempts.Select(a => a.Score / Math.Max(1, a.Quiz?.TotalMarks ?? 100) * 100).DefaultIfEmpty(0).Average()
                };
            }).ToList();

            vm.PopularCourses = await _db.Courses
                .Where(c => c.Status == "Published")
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .OrderByDescending(c => c.Enrollments.Count(e => e.Status == "Approved"))
                .Take(3)
                .Select(c => new DashboardCourseVM
                {
                    Id = c.Id,
                    Title = c.Title,
                    Instructor = c.Instructor != null ? c.Instructor.FullName : "Instructor",
                    EnrollmentCount = c.Enrollments.Count(e => e.Status == "Approved")
                })
                .ToListAsync();
            var publishedCourses = await _db.Courses
                .Where(c => c.Status == "Published" && c.Instructor != null)
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .ToListAsync();
            vm.TopInstructors = publishedCourses
                .GroupBy(c => new { c.InstructorId, c.Instructor!.FullName })
                .Select(g => new TopInstructorVM
                {
                    Name = g.Key.FullName,
                    Courses = g.Count(),
                    Students = g.Sum(c => c.Enrollments.Count(e => e.Status == "Approved"))
                })
                .OrderByDescending(i => i.Students)
                .Take(5)
                .ToList();
            vm.EnrolledCourses = vm.Courses.Count;
            vm.PendingAssignments = vm.Courses.Sum(c => c.AssignmentPending);
            vm.PendingQuizzes = vm.Courses.Sum(c => c.QuizPending);
            vm.AssignmentMarks = submissions.Where(s => s.Grade.HasValue).Select(s => s.Grade!.Value).DefaultIfEmpty(0).Average();
            vm.QuizMarks = attempts.Select(a => a.Score / Math.Max(1, a.Quiz?.TotalMarks ?? 100) * 100).DefaultIfEmpty(0).Average();
            vm.Completion = vm.Courses.Select(c => (double)c.Progress).DefaultIfEmpty(0).Average();
        }
        else if (role == "Instructor")
        {
            vm.Courses = await _db.Courses
                .Where(c => c.InstructorId == user.Id)
                .Include(c => c.Enrollments)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new DashboardCourseVM
                {
                    Id = c.Id,
                    Title = c.Title,
                    Status = c.Status,
                    EnrollmentCount = c.Enrollments.Count(e => e.Status == "Approved")
                })
                .ToListAsync();
            vm.PendingReviews = await _db.AssignmentSubmissions
                .Where(s => s.Assignment!.Course!.InstructorId == user.Id && s.Grade == null)
                .Include(s => s.Assignment).ThenInclude(a => a!.Course)
                .Include(s => s.Student)
                .OrderByDescending(s => s.SubmittedAt)
                .Take(6)
                .Select(s => new PendingSubmissionVM
                {
                    AssignmentTitle = s.Assignment!.Title,
                    CourseTitle = s.Assignment.Course!.Title,
                    StudentName = s.Student != null ? s.Student.FullName : "Student"
                })
                .ToListAsync();
            var instructorEnrollments = await _db.Enrollments
                .Where(e => e.Status == "Approved" && e.Course!.InstructorId == user.Id && e.Student != null)
                .Include(e => e.Student)
                .ToListAsync();
            vm.TopStudents = instructorEnrollments
                .GroupBy(e => new { e.StudentId, e.Student!.FullName })
                .Select(g => new InstructorStudentVM { Name = g.Key.FullName, Courses = g.Count() })
                .OrderByDescending(s => s.Courses)
                .Take(6)
                .ToList();
            vm.TotalCourses = vm.Courses.Count;
            vm.TotalStudents = vm.Courses.Sum(c => c.EnrollmentCount);
            vm.PendingSubmissions = vm.PendingReviews.Count;
        }

        ViewData["Title"] = "Dashboard";
        return View(vm);
    }
}
