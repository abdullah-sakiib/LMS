using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class AssignmentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public AssignmentController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public async Task<IActionResult> Index(int? courseId)
    {
        var userId = _userManager.GetUserId(User)!;
        var isStudent = User.IsInRole("Student");
        IQueryable<Assignment> query = _db.Assignments.Include(item => item.Course);

        if (isStudent)
        {
            query = query.Where(item => item.Course != null && item.Course.Status == "Published" &&
                item.Course.Enrollments.Any(enrollment => enrollment.StudentId == userId && enrollment.Status == "Approved"));
        }
        else if (User.IsInRole("Instructor"))
        {
            query = query.Where(item => item.Course != null && item.Course.InstructorId == userId);
        }

        if (courseId.HasValue)
        {
            if (!isStudent && !await CanManageCourseAsync(courseId.Value)) return NotFound();
            query = query.Where(item => item.CourseId == courseId.Value);
        }

        var assignments = await query.OrderBy(item => item.Deadline).ThenBy(item => item.Title).ToListAsync();
        var submissions = isStudent
            ? await _db.AssignmentSubmissions.Where(item => item.StudentId == userId &&
                    assignments.Select(assignment => assignment.Id).Contains(item.AssignmentId))
                .ToDictionaryAsync(item => item.AssignmentId)
            : new Dictionary<int, AssignmentSubmission>();

        ViewData["Title"] = "Assignments";
        ViewData["PageTitle"] = "Assignments";
        ViewData["PageSubtitle"] = "Track and submit assignment work";
        return View(new AssignmentWorkspaceVM
        {
            IsStudent = isStudent,
            Rows = assignments.Select(assignment => new AssignmentRowVM
            {
                Assignment = assignment,
                Submission = submissions.GetValueOrDefault(assignment.Id)
            }).ToList()
        });
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create(int courseId)
    {
        if (!await CanManageCourseAsync(courseId)) return NotFound();
        return View(new AssignmentVM { CourseId = courseId });
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create(AssignmentVM vm, IFormFile? attachment)
    {
        if (!ModelState.IsValid) return View(vm);
        if (!await CanManageCourseAsync(vm.CourseId)) return NotFound();

        var assignment = new Assignment
        {
            CourseId = vm.CourseId,
            Title = vm.Title.Trim(),
            Description = vm.Description.Trim(),
            Deadline = DateTime.SpecifyKind(vm.Deadline, DateTimeKind.Local).ToUniversalTime(),
            MaxScore = Math.Max(1, vm.MaxScore)
        };
        await SaveAttachmentAsync(assignment, attachment);
        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { courseId = vm.CourseId });
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var assignment = await _db.Assignments.FindAsync(id);
        if (assignment == null || !await CanManageCourseAsync(assignment.CourseId)) return NotFound();
        return View(new AssignmentVM
        {
            Id = assignment.Id,
            CourseId = assignment.CourseId,
            Title = assignment.Title,
            Description = assignment.Description,
            Deadline = assignment.Deadline.ToLocalTime(),
            MaxScore = assignment.MaxScore
        });
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Edit(AssignmentVM vm, IFormFile? attachment)
    {
        if (!ModelState.IsValid) return View(vm);
        var assignment = await _db.Assignments.FindAsync(vm.Id);
        if (assignment == null || !await CanManageCourseAsync(assignment.CourseId)) return NotFound();
        assignment.Title = vm.Title.Trim();
        assignment.Description = vm.Description.Trim();
        assignment.Deadline = DateTime.SpecifyKind(vm.Deadline, DateTimeKind.Local).ToUniversalTime();
        assignment.MaxScore = Math.Max(1, vm.MaxScore);
        await SaveAttachmentAsync(assignment, attachment);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { courseId = vm.CourseId });
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var assignment = await _db.Assignments.FindAsync(id);
        if (assignment == null || !await CanManageCourseAsync(assignment.CourseId)) return NotFound();
        var courseId = assignment.CourseId;
        _db.Assignments.Remove(assignment);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { courseId });
    }

    public async Task<IActionResult> Details(int id)
    {
        var assignment = await _db.Assignments.Include(item => item.Course)
            .Include(item => item.Submissions).ThenInclude(submission => submission.Student)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (assignment == null || !await CanAccessAssignmentAsync(assignment)) return NotFound();
        var userId = _userManager.GetUserId(User);
        ViewBag.IsInstructor = !User.IsInRole("Student");
        ViewBag.ExistingSubmission = assignment.Submissions.FirstOrDefault(item => item.StudentId == userId);
        return View(assignment);
    }

    [HttpPost, Authorize(Roles = "Student")]
    public async Task<IActionResult> Submit(int assignmentId, IFormFile? file, string? comment, bool returnToWorkspace = true)
    {
        var assignment = await _db.Assignments.Include(item => item.Course)
            .FirstOrDefaultAsync(item => item.Id == assignmentId);
        if (assignment == null || !await CanAccessAssignmentAsync(assignment)) return NotFound();
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file before submitting.";
            return RedirectAfterSubmit(assignmentId, returnToWorkspace);
        }

        var userId = _userManager.GetUserId(User)!;
        var fileInfo = await SaveUploadAsync(file, "submissions");
        var submission = await _db.AssignmentSubmissions
            .FirstOrDefaultAsync(item => item.AssignmentId == assignmentId && item.StudentId == userId);
        if (submission == null)
        {
            submission = new AssignmentSubmission { AssignmentId = assignmentId, StudentId = userId };
            _db.AssignmentSubmissions.Add(submission);
        }
        submission.FilePath = fileInfo.Path;
        submission.OriginalFileName = file.FileName;
        submission.Comment = comment?.Trim() ?? string.Empty;
        submission.SubmittedAt = DateTime.UtcNow;
        submission.UpdatedAt = DateTime.UtcNow;
        submission.Status = submission.SubmittedAt > assignment.Deadline ? "Late" : "Submitted";
        submission.Grade = null;
        submission.Feedback = null;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Submitted {assignment.Title} successfully.";
        return RedirectAfterSubmit(assignmentId, returnToWorkspace);
    }

    private IActionResult RedirectAfterSubmit(int assignmentId, bool workspace) =>
        workspace ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Details), new { id = assignmentId });

    private async Task SaveAttachmentAsync(Assignment assignment, IFormFile? attachment)
    {
        if (attachment == null || attachment.Length == 0) return;
        var fileInfo = await SaveUploadAsync(attachment, "assignments");
        assignment.AttachmentPath = fileInfo.Path;
        assignment.AttachmentFileName = attachment.FileName;
    }

    private async Task<(string Path, string Name)> SaveUploadAsync(IFormFile file, string directory)
    {
        var uploads = Path.Combine(_env.WebRootPath, "uploads", directory);
        Directory.CreateDirectory(uploads);
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        using var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create);
        await file.CopyToAsync(stream);
        return ($"/uploads/{directory}/{fileName}", fileName);
    }

    private async Task<bool> CanAccessAssignmentAsync(Assignment assignment)
    {
        if (!User.IsInRole("Student")) return await CanManageCourseAsync(assignment.CourseId);
        var userId = _userManager.GetUserId(User);
        return await _db.Enrollments.AnyAsync(item => item.CourseId == assignment.CourseId &&
            item.StudentId == userId && item.Status == "Approved");
    }

    private async Task<bool> CanManageCourseAsync(int courseId)
    {
        if (User.IsInRole("Admin")) return await _db.Courses.AnyAsync(item => item.Id == courseId);
        var userId = _userManager.GetUserId(User);
        return await _db.Courses.AnyAsync(item => item.Id == courseId && item.InstructorId == userId);
    }
}
