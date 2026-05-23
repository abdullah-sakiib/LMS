using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly string[] AllowedRoles = ["Admin", "Instructor", "Student"];
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // Dashboard with site stats
    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var users = await _userManager.Users.OrderByDescending(u => u.CreatedAt).Take(6).ToListAsync();
        var recentUsers = new List<AdminUserRowVM>();
        foreach (var user in users)
        {
            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Student";
            recentUsers.Add(new AdminUserRowVM
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = role,
                JoinedAt = user.CreatedAt
            });
        }
        var students = await _userManager.GetUsersInRoleAsync("Student");
        var instructors = await _userManager.GetUsersInRoleAsync("Instructor");
        var today = DateTime.UtcNow.Date;
        var registrationDates = await _userManager.Users
            .Where(u => u.CreatedAt >= today.AddDays(-6))
            .Select(u => u.CreatedAt.Date)
            .ToListAsync();

        var vm = new AdminDashboardVM
        {
            FullName = currentUser?.FullName ?? "Administrator",
            TotalUsers = await _userManager.Users.CountAsync(),
            TotalCourses = await _db.Courses.CountAsync(),
            TotalEnrollments = await _db.Enrollments.CountAsync(),
            TotalSubmissions = await _db.AssignmentSubmissions.CountAsync(),
            TotalQuizAttempts = await _db.QuizAttempts.CountAsync(),
            Students = students.Count,
            Instructors = instructors.Count,
            RegistrationsByDay = Enumerable.Range(0, 7)
                .Select(offset => registrationDates.Count(date => date == today.AddDays(offset - 6)))
                .ToList(),
            RecentUsers = recentUsers,
            Courses = await _db.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new AdminCourseRowVM
                {
                    Id = c.Id,
                    Title = c.Title,
                    Instructor = c.Instructor != null ? c.Instructor.FullName : "Instructor",
                    Students = c.Enrollments.Count(e => e.Status == "Approved"),
                    Status = c.Status
                })
                .ToListAsync()
        };
        return View(vm);
    }

    // User list
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();
        var userRoles = new Dictionary<string, IList<string>>();
        foreach (var u in users)
            userRoles[u.Id] = await _userManager.GetRolesAsync(u);

        ViewBag.UserRoles = userRoles;
        return View(users);
    }

    // POST: Change user role
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string userId, string newRole)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (!AllowedRoles.Contains(newRole, StringComparer.Ordinal))
        {
            TempData["Error"] = "Select a valid role.";
            return RedirectToAction(nameof(Users));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains(newRole))
        {
            TempData["Success"] = $"Role is already set to {newRole}.";
            return RedirectToAction(nameof(Users));
        }

        if (currentRoles.Contains("Admin") && newRole != "Admin")
        {
            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You cannot remove your own administrator role.";
                return RedirectToAction(nameof(Users));
            }

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
            {
                TempData["Error"] = "At least one administrator account must remain.";
                return RedirectToAction(nameof(Users));
            }
        }

        if (!await _roleManager.RoleExistsAsync(newRole))
            await _roleManager.CreateAsync(new IdentityRole(newRole));

        var addResult = await _userManager.AddToRoleAsync(user, newRole);
        if (!addResult.Succeeded)
        {
            TempData["Error"] = string.Join(" ", addResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Users));
        }

        var oldRoles = currentRoles.Where(role => role != newRole).ToArray();
        if (oldRoles.Length > 0)
            await _userManager.RemoveFromRolesAsync(user, oldRoles);

        TempData["Success"] = $"Role updated to {newRole}.";
        return RedirectToAction(nameof(Users));
    }

    // POST: Delete user
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(Users));
        }

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
            {
                TempData["Error"] = "At least one administrator account must remain.";
                return RedirectToAction(nameof(Users));
            }
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Users));
        }

        TempData["Success"] = "User deleted.";
        return RedirectToAction(nameof(Users));
    }

    // Course list
    public async Task<IActionResult> Courses()
    {
        var courses = await _db.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Enrollments)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return View(courses);
    }

    // POST: Delete course
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCourse(int courseId)
    {
        var course = await _db.Courses.FindAsync(courseId);
        if (course == null) return NotFound();
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course deleted.";
        return RedirectToAction(nameof(Courses));
    }
}
