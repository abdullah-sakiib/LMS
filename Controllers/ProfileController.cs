using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _environment;

    public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, IWebHostEnvironment environment)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        return View(await BuildViewModelAsync(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string formType, string? firstName, string? lastName, string? email,
        string? phone, string? city, string? country, string? bio, string? department, string? university,
        string? designation, string? currentPassword, string? newPassword1, string? newPassword2, IFormFile? avatar)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        switch (formType)
        {
            case "photo":
            {
                if (avatar is not { Length: > 0 })
                {
                    TempData["Error"] = "Select a profile photo to upload.";
                    return RedirectToAction(nameof(Index));
                }

                if (!IsValidAvatar(avatar))
                {
                    TempData["Error"] = "Profile photos must be JPG, PNG, GIF, or WEBP files up to 2MB.";
                    return RedirectToAction(nameof(Index));
                }

                user.ProfilePicture = await StoreAvatarAsync(avatar);
                var photoUpdated = await _userManager.UpdateAsync(user);
                if (!photoUpdated.Succeeded)
                {
                    TempData["Error"] = string.Join(" ", photoUpdated.Errors.Select(error => error.Description));
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = "Profile photo updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            case "general":
                if (string.IsNullOrWhiteSpace(email))
                {
                    TempData["Error"] = "Email is required.";
                    return RedirectToAction(nameof(Index));
                }

                var emailOwner = await _userManager.FindByEmailAsync(email.Trim());
                if (emailOwner != null && emailOwner.Id != user.Id)
                {
                    TempData["Error"] = "This email is already used by another account.";
                    return RedirectToAction(nameof(Index));
                }

                user.FirstName = (firstName ?? string.Empty).Trim();
                user.LastName = (lastName ?? string.Empty).Trim();
                user.FullName = $"{user.FirstName} {user.LastName}".Trim();
                user.City = Limit(city, 120);
                user.Country = Limit(country, 120);
                user.Bio = Limit(bio, 500);
                user.PhoneNumber = Limit(phone, 30);
                user.Email = email.Trim();
                user.UserName = user.Email;

                if (avatar is { Length: > 0 })
                {
                    if (!IsValidAvatar(avatar))
                    {
                        TempData["Error"] = "Profile photos must be JPG, PNG, GIF, or WEBP files up to 2MB.";
                        return RedirectToAction(nameof(Index));
                    }

                    user.ProfilePicture = await StoreAvatarAsync(avatar);
                }

                var updated = await _userManager.UpdateAsync(user);
                if (!updated.Succeeded)
                {
                    TempData["Error"] = string.Join(" ", updated.Errors.Select(error => error.Description));
                    return RedirectToAction(nameof(Index));
                }
                TempData["Success"] = "Profile updated successfully.";
                break;

            case "academic":
                if (User.IsInRole("Student"))
                    user.Department = Limit(department, 120);
                else
                {
                    user.University = Limit(university, 180);
                    user.Designation = Limit(designation, 120);
                }
                await _userManager.UpdateAsync(user);
                TempData["Success"] = "Academic profile updated successfully.";
                break;

            case "security":
                if (string.IsNullOrEmpty(newPassword1) || newPassword1.Length < 8)
                {
                    TempData["Error"] = "New password must be at least 8 characters long.";
                    return RedirectToAction(nameof(Index), new { tab = "security" });
                }
                if (newPassword1 != newPassword2)
                {
                    TempData["Error"] = "New passwords do not match.";
                    return RedirectToAction(nameof(Index), new { tab = "security" });
                }

                var passwordResult = await _userManager.ChangePasswordAsync(user, currentPassword ?? string.Empty, newPassword1);
                if (!passwordResult.Succeeded)
                {
                    TempData["Error"] = string.Join(" ", passwordResult.Errors.Select(error => error.Description));
                    return RedirectToAction(nameof(Index), new { tab = "security" });
                }
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Password updated successfully.";
                return RedirectToAction(nameof(Index), new { tab = "security" });
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<ProfileVM> BuildViewModelAsync(ApplicationUser user)
    {
        var role = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Instructor") ? "Instructor" : "Student";
        var vm = new ProfileVM
        {
            Profile = user,
            DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "User" : user.FullName,
            Role = role,
            MemberCode = $"{(role == "Student" ? "STU" : "TCH")}-{user.Id[..Math.Min(8, user.Id.Length)].ToUpperInvariant()}"
        };

        if (role == "Student")
        {
            var enrollments = await _db.Enrollments
                .Where(item => item.StudentId == user.Id && item.Status == "Approved")
                .Include(item => item.Course).ThenInclude(course => course!.Instructor)
                .Include(item => item.Course).ThenInclude(course => course!.Modules).ThenInclude(module => module.ContentItems)
                .ToListAsync();
            var courseIds = enrollments.Select(item => item.CourseId).ToList();
            var completed = await _db.StudentProgress
                .Where(item => item.StudentId == user.Id && item.IsCompleted && courseIds.Contains(item.CourseId))
                .Select(item => new { item.CourseId, item.ContentItemId }).ToListAsync();
            var percentages = new List<double>();

            foreach (var enrollment in enrollments.Where(item => item.Course != null))
            {
                var course = enrollment.Course!;
                var totalContent = course.Modules.Sum(module => module.ContentItems.Count);
                var complete = completed.Count(item => item.CourseId == course.Id);
                var progress = totalContent == 0 ? 0 : Math.Round(complete * 100d / totalContent, 1);
                var assessmentPercentages = new List<double>();
                assessmentPercentages.AddRange(await _db.AssignmentSubmissions
                    .Where(item => item.StudentId == user.Id && item.Assignment!.CourseId == course.Id && item.Grade.HasValue)
                    .Select(item => item.Assignment!.MaxScore > 0 ? item.Grade!.Value * 100d / item.Assignment.MaxScore : 0)
                    .ToListAsync());
                assessmentPercentages.AddRange(await _db.QuizAttempts
                    .Where(item => item.StudentId == user.Id && item.Quiz!.CourseId == course.Id)
                    .Select(item => item.Quiz!.TotalMarks > 0 ? item.Score * 100d / item.Quiz.TotalMarks : 0)
                    .ToListAsync());
                if (assessmentPercentages.Count > 0)
                {
                    percentages.AddRange(assessmentPercentages);
                    vm.GradeSummary.Add(new ProfileGradeVM
                    {
                        Course = course.Title,
                        AveragePercent = Math.Round(assessmentPercentages.Average(), 1)
                    });
                }
                var certificateAvailable = course.IsEnded;
                vm.Courses.Add(new ProfileCourseVM
                {
                    Id = course.Id,
                    Title = course.Title,
                    Subtitle = course.Instructor?.FullName ?? "TBA",
                    Progress = progress,
                    IsCourseEnded = course.IsEnded,
                    CertificateAvailable = certificateAvailable
                });
            }

            vm.CourseCount = vm.Courses.Count;
            vm.Certificates = vm.Courses.Count(item => item.CertificateAvailable);
            vm.Submitted = await _db.AssignmentSubmissions.CountAsync(item => item.StudentId == user.Id);
            vm.AverageGrade = percentages.Count == 0 ? 0 : Math.Round(percentages.Average(), 1);
            vm.GradeSummary = vm.GradeSummary.OrderByDescending(item => item.AveragePercent).Take(6).ToList();
        }
        else if (role == "Instructor")
        {
            var courses = await _db.Courses
                .Where(item => item.InstructorId == user.Id)
                .Include(item => item.Instructor)
                .Include(item => item.Enrollments)
                .Include(item => item.Reviews)
                .OrderBy(item => item.Title)
                .ToListAsync();
            vm.Courses = courses.Select(course => new ProfileCourseVM
            {
                Id = course.Id,
                Title = course.Title,
                Subtitle = course.Instructor?.FullName ?? "TBA",
                StudentCount = course.Enrollments.Count(item => item.Status == "Approved"),
                Rating = course.Reviews.Count == 0 ? 0 : Math.Round(course.Reviews.Average(item => item.InstructorRating), 1)
            }).ToList();
            vm.CourseCount = courses.Count;
            vm.TotalStudents = courses.SelectMany(item => item.Enrollments)
                .Where(item => item.Status == "Approved").Select(item => item.StudentId).Distinct().Count();
            var reviewedCourses = vm.Courses.Where(item => item.Rating > 0).ToList();
            vm.RatedCourses = reviewedCourses.Count;
            vm.AverageRating = reviewedCourses.Count == 0 ? 0 : Math.Round(reviewedCourses.Average(item => item.Rating), 1);
        }

        return vm;
    }

    private static string? Limit(string? value, int length)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrEmpty(cleaned)) return null;
        return cleaned.Length <= length ? cleaned : cleaned[..length];
    }

    private static bool IsValidAvatar(IFormFile avatar)
    {
        return avatar.Length <= 2 * 1024 * 1024
            && ImageExtensions.Contains(Path.GetExtension(avatar.FileName).ToLowerInvariant());
    }

    private async Task<string> StoreAvatarAsync(IFormFile avatar)
    {
        var uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadDirectory);
        var filename = $"{Guid.NewGuid():N}{Path.GetExtension(avatar.FileName).ToLowerInvariant()}";
        await using var stream = System.IO.File.Create(Path.Combine(uploadDirectory, filename));
        await avatar.CopyToAsync(stream);
        return $"/uploads/avatars/{filename}";
    }
}
