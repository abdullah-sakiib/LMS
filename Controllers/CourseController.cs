using System.Text.RegularExpressions;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class CourseController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CourseController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var userId = _userManager.GetUserId(User);
        var isManagementView = User.IsInRole("Instructor") || User.IsInRole("Admin");

        IQueryable<Course> query = _db.Courses
            .Include(course => course.Instructor)
            .Include(course => course.Enrollments).ThenInclude(enrollment => enrollment.Student)
            .Include(course => course.Modules).ThenInclude(module => module.ContentItems)
            .Include(course => course.Assignments)
            .Include(course => course.Reviews);

        if (User.IsInRole("Instructor"))
            query = query.Where(course => course.InstructorId == userId);
        else if (!User.IsInRole("Admin"))
            query = query.Where(course => course.Status == "Published");

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(course =>
                course.Title.ToLower().Contains(term) ||
                course.Description.ToLower().Contains(term) ||
                course.Tags.ToLower().Contains(term) ||
                course.Category.ToLower().Contains(term));
        }

        var courses = await query.OrderBy(course => course.Title).ToListAsync();
        var cards = await BuildCardsAsync(courses);
        var defaultCategories = new List<string>
        {
            "Computer Science",
            "Data Science",
            "Business",
            "Design",
            "Marketing",
            "Languages",
            "Engineering",
            "Health",
            "Arts",
            "Mathematics",
            "IT & Networking"
        };
        var viewModel = new CourseIndexVM
        {
            IsManagementView = isManagementView,
            SearchQuery = q?.Trim() ?? string.Empty,
            Courses = cards,
            Categories = courses.Select(course => course.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Concat(defaultCategories)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(category => category).ToList(),
            Tags = courses.SelectMany(course => course.TagList)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag).ToList()
        };

        if (userId != null)
        {
            viewModel.EnrolledCourseIds = (await _db.Enrollments
                .Where(enrollment => enrollment.StudentId == userId && enrollment.Status == "Approved")
                .Select(enrollment => enrollment.CourseId)
                .ToListAsync()).ToHashSet();
        }

        ViewData["Title"] = isManagementView ? "My Courses" : "Course Catalog";
        return View(viewModel);
    }

    public async Task<IActionResult> Details(int id)
    {
        var course = await _db.Courses
            .Include(item => item.Instructor)
            .Include(item => item.Modules).ThenInclude(module => module.ContentItems)
            .Include(item => item.Enrollments)
            .Include(item => item.Reviews).ThenInclude(review => review.Student)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (course == null || (course.Status != "Published" && !await CanManageCourseAsync(course)))
            return NotFound();

        var userId = _userManager.GetUserId(User);
        var card = (await BuildCardsAsync(new List<Course> { course })).Single();
        var viewModel = new CourseDetailsVM
        {
            Course = card.Course,
            ApprovedEnrollmentCount = card.ApprovedEnrollmentCount,
            PendingEnrollmentCount = card.PendingEnrollmentCount,
            LectureCount = card.LectureCount,
            CourseRating = card.CourseRating,
            InstructorRating = card.InstructorRating,
            CourseReviewCount = card.CourseReviewCount,
            InstructorReviewCount = card.InstructorReviewCount,
            UserId = userId,
            IsCourseStaff = await CanManageCourseAsync(course),
            IsStudent = User.IsInRole("Student"),
            Enrollment = course.Enrollments.FirstOrDefault(enrollment => enrollment.StudentId == userId),
            MyReview = course.Reviews.FirstOrDefault(review => review.StudentId == userId),
            RecentReviews = course.Reviews.OrderByDescending(review => review.UpdatedAt).Take(4).ToList(),
            RelatedCourses = await _db.Courses
                .Where(item => item.Id != course.Id && item.Status == "Published")
                .OrderBy(item => item.Title).Take(2).ToListAsync(),
            ResourceCount = course.Modules.Sum(module => module.ContentItems.Count)
        };

        ViewData["Title"] = course.Title;
        return View(viewModel);
    }

    public async Task<IActionResult> Content(int id, int? resourceId)
    {
        var course = await _db.Courses
            .Include(item => item.Modules).ThenInclude(module => module.ContentItems)
            .FirstOrDefaultAsync(item => item.Id == id && item.Status == "Published");
        if (course == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        var isStudent = User.IsInRole("Student");
        var enrollment = userId == null ? null : await _db.Enrollments
            .FirstOrDefaultAsync(item => item.CourseId == id && item.StudentId == userId && item.Status == "Approved");
        if (isStudent && enrollment == null)
        {
            TempData["Error"] = "Please enroll in the course first.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!isStudent && !await CanManageCourseAsync(course)) return NotFound();

        var resources = course.Modules.OrderBy(module => module.Order)
            .SelectMany(module => module.ContentItems.OrderBy(item => item.Order)).ToList();
        var completedIds = isStudent && userId != null
            ? (await _db.StudentProgress.Where(progress => progress.StudentId == userId &&
                    progress.CourseId == id && progress.IsCompleted)
                .Select(progress => progress.ContentItemId).ToListAsync()).ToHashSet()
            : new HashSet<int>();
        var selected = resources.FirstOrDefault(item => item.Id == resourceId) ?? resources.FirstOrDefault();

        var viewModel = new CourseContentVM
        {
            Course = course,
            Modules = course.Modules.OrderBy(module => module.Order).ToList(),
            SelectedResource = selected,
            CompletedResourceIds = completedIds,
            IsStudent = isStudent,
            TotalResources = resources.Count,
            VideoResources = resources.Count(item => item.Type == "Video"),
            ProgressPercent = resources.Count == 0 ? 0 : Math.Round(completedIds.Count * 100d / resources.Count, 1),
            CertificateAvailable = isStudent && course.IsEnded && resources.Count > 0 && completedIds.Count == resources.Count
        };
        ViewData["Title"] = $"{course.Title} Content";
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Discussion(int id)
    {
        var course = await GetDiscussionCourseAsync(id);
        if (course == null) return NotFound();
        var canPost = await CanAccessDiscussionAsync(course);
        if (!canPost) return RedirectToAction(nameof(Details), new { id });

        ViewData["Title"] = $"Discussion - {course.Title}";
        return View(new CourseDiscussionVM
        {
            Course = course,
            CanPost = true,
            Posts = await _db.CourseDiscussionPosts.Include(post => post.Author)
                .Where(post => post.CourseId == id).OrderByDescending(post => post.CreatedAt).ToListAsync()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Discussion(int id, string message)
    {
        var course = await GetDiscussionCourseAsync(id);
        if (course == null) return NotFound();
        if (!await CanAccessDiscussionAsync(course)) return RedirectToAction(nameof(Details), new { id });
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["Error"] = "Message cannot be empty.";
            return RedirectToAction(nameof(Discussion), new { id });
        }

        _db.CourseDiscussionPosts.Add(new CourseDiscussionPost
        {
            CourseId = id,
            AuthorId = _userManager.GetUserId(User)!,
            Message = message.Trim()[..Math.Min(message.Trim().Length, 2000)]
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message posted.";
        return RedirectToAction(nameof(Discussion), new { id });
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitReview(int id, int courseRating, int instructorRating, string? comment)
    {
        var userId = _userManager.GetUserId(User)!;
        var canReview = await _db.Enrollments.AnyAsync(enrollment => enrollment.CourseId == id &&
            enrollment.StudentId == userId && enrollment.Status == "Approved");
        if (!canReview || courseRating is < 1 or > 5 || instructorRating is < 1 or > 5)
        {
            TempData["Error"] = canReview
                ? "Ratings must be between 1 and 5 stars."
                : "You can only review courses you are enrolled in.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var review = await _db.CourseReviews
            .FirstOrDefaultAsync(item => item.CourseId == id && item.StudentId == userId);
        if (review == null)
        {
            review = new CourseReview { CourseId = id, StudentId = userId, CreatedAt = DateTime.UtcNow };
            _db.CourseReviews.Add(review);
        }
        review.CourseRating = courseRating;
        review.InstructorRating = instructorRating;
        review.Comment = comment?.Trim() ?? string.Empty;
        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Thanks! Your review has been saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Certificate(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var course = await _db.Courses.Include(item => item.Instructor)
            .Include(item => item.Modules).ThenInclude(module => module.ContentItems)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (course == null) return NotFound();

        var isEnrolled = await _db.Enrollments.AnyAsync(item => item.CourseId == id &&
            item.StudentId == user!.Id && item.Status == "Approved");
        var totalResources = course.Modules.Sum(module => module.ContentItems.Count);
        var completedResources = await _db.StudentProgress.CountAsync(item => item.CourseId == id &&
            item.StudentId == user!.Id && item.IsCompleted);
        if (!isEnrolled || !course.IsEnded || totalResources == 0 || completedResources != totalResources)
        {
            TempData["Error"] = !isEnrolled
                ? "You must be enrolled in this course to download a certificate."
                : !course.IsEnded
                    ? "This course is not ended yet. Certificate is not available."
                    : totalResources == 0
                        ? "Certificate is locked until course resources are available and completed."
                        : $"Certificate is locked. Complete all resources first ({completedResources}/{totalResources}).";
            return RedirectToAction(nameof(Content), new { id });
        }

        var issuedOn = DateTime.UtcNow;
        var safeTitle = Regex.Replace(course.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{safeTitle}-certificate.html\"";
        return View(new CourseCertificateVM
        {
            Course = course,
            StudentName = user!.FullName,
            InstructorName = course.Instructor?.FullName ?? "Course Instructor",
            IssuedOn = issuedOn,
            CertificateCode = $"EDU-{course.Id:00000}-{user.Id[..Math.Min(user.Id.Length, 5)].ToUpperInvariant()}-{issuedOn:yyyyMMdd}"
        });
    }

    [Authorize(Roles = "Instructor,Admin")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create Course";
        return View(new CourseVM());
    }

    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create(CourseVM model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        var course = new Course { InstructorId = user!.Id };
        ApplyCourseFields(course, model);
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null || !await CanManageCourseAsync(course)) return NotFound();
        ViewData["Title"] = "Edit Course";
        return View(new CourseVM
        {
            Id = course.Id,
            Title = course.Title,
            ShortDescription = course.ShortDescription,
            Description = course.Description,
            Category = course.Category,
            Tags = course.Tags,
            Level = course.Level,
            DurationWeeks = course.DurationWeeks,
            Capacity = course.Capacity,
            EnrollmentType = course.EnrollmentType,
            AllowDiscussions = course.AllowDiscussions,
            Status = course.Status
        });
    }

    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Edit(CourseVM model)
    {
        if (!ModelState.IsValid) return View(model);
        var course = await _db.Courses.FindAsync(model.Id);
        if (course == null || !await CanManageCourseAsync(course)) return NotFound();
        ApplyCourseFields(course, model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> EndCourse(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null || !await CanManageCourseAsync(course)) return NotFound();
        course.IsEnded = true;
        course.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course ended. Eligible students can now download certificates.";
        return RedirectToAction("Manage", "Content", new { courseId = id });
    }

    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course != null && await CanManageCourseAsync(course))
        {
            _db.Courses.Remove(course);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course != null && await CanManageCourseAsync(course) &&
            new[] { "Draft", "Published", "Archived" }.Contains(status))
        {
            course.Status = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<CourseCardVM>> BuildCardsAsync(List<Course> courses)
    {
        var instructorIds = courses.Select(course => course.InstructorId).Distinct().ToList();
        var instructorReviews = await _db.CourseReviews
            .Where(review => review.Course != null && instructorIds.Contains(review.Course.InstructorId))
            .GroupBy(review => review.Course!.InstructorId)
            .Select(group => new { InstructorId = group.Key, Rating = group.Average(review => review.InstructorRating), Count = group.Count() })
            .ToDictionaryAsync(group => group.InstructorId);

        var courseIds = courses.Select(course => course.Id).ToList();
        var quizCounts = await _db.Quizzes.Where(quiz => courseIds.Contains(quiz.CourseId))
            .GroupBy(quiz => quiz.CourseId).ToDictionaryAsync(group => group.Key, group => group.Count());

        return courses.Select(course =>
        {
            instructorReviews.TryGetValue(course.InstructorId, out var instructorStats);
            return new CourseCardVM
            {
                Course = course,
                ApprovedEnrollmentCount = course.Enrollments.Count(enrollment => enrollment.Status == "Approved"),
                PendingEnrollmentCount = course.Enrollments.Count(enrollment => enrollment.Status == "Pending"),
                LectureCount = course.Modules.Sum(module => module.ContentItems.Count),
                QuizCount = quizCounts.TryGetValue(course.Id, out var quizCount) ? quizCount : 0,
                CourseRating = course.Reviews.Count == 0 ? 0 : Math.Round(course.Reviews.Average(review => review.CourseRating), 1),
                CourseReviewCount = course.Reviews.Count,
                InstructorRating = instructorStats == null ? 0 : Math.Round(instructorStats.Rating, 1),
                InstructorReviewCount = instructorStats?.Count ?? 0
            };
        }).ToList();
    }

    private static void ApplyCourseFields(Course course, CourseVM model)
    {
        course.Title = model.Title.Trim();
        course.ShortDescription = model.ShortDescription.Trim();
        course.Description = model.Description.Trim();
        course.Category = model.Category.Trim();
        course.Tags = model.Tags.Trim();
        course.Level = new[] { "Beginner", "Intermediate", "Advanced" }.Contains(model.Level) ? model.Level : "Beginner";
        course.DurationWeeks = Math.Max(model.DurationWeeks, 1);
        course.Capacity = Math.Max(model.Capacity, 1);
        course.EnrollmentType = model.EnrollmentType == "Approval" ? "Approval" : "Open";
        course.AllowDiscussions = model.AllowDiscussions;
        course.Status = new[] { "Draft", "Published", "Archived" }.Contains(model.Status) ? model.Status : "Draft";
    }

    private async Task<Course?> GetDiscussionCourseAsync(int id) =>
        await _db.Courses.Include(course => course.Instructor)
            .FirstOrDefaultAsync(course => course.Id == id && course.Status == "Published");

    private async Task<bool> CanAccessDiscussionAsync(Course course)
    {
        if (await CanManageCourseAsync(course)) return true;
        if (!User.IsInRole("Student")) return false;
        if (!course.AllowDiscussions)
        {
            TempData["Error"] = "Discussion is disabled for this course.";
            return false;
        }
        var userId = _userManager.GetUserId(User);
        var enrolled = await _db.Enrollments.AnyAsync(enrollment => enrollment.CourseId == course.Id &&
            enrollment.StudentId == userId && enrollment.Status == "Approved");
        if (!enrolled) TempData["Error"] = "You can join discussion only after enrollment approval.";
        return enrolled;
    }

    private async Task<bool> CanManageCourseAsync(Course course)
    {
        if (User.IsInRole("Admin")) return true;
        return User.IsInRole("Instructor") && course.InstructorId == _userManager.GetUserId(User);
    }
}
