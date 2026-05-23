using LMS.Data;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize(Roles = "Instructor,Admin")]
public class ContentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public ContentController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    // Add Module
    [HttpPost]
    public async Task<IActionResult> AddModule(int courseId, string title)
    {
        if (!await CanManageCourseAsync(courseId) || string.IsNullOrWhiteSpace(title)) return NotFound();
        var order = await _db.Modules.CountAsync(m => m.CourseId == courseId);
        _db.Modules.Add(new Module
        {
            CourseId = courseId,
            Title = title,
            Order = order + 1
        });
        await _db.SaveChangesAsync();
        return RedirectToAction("Manage", new { courseId });
    }

    // Delete Module
    [HttpPost]
    public async Task<IActionResult> DeleteModule(int moduleId, int courseId)
    {
        if (!await CanManageCourseAsync(courseId)) return NotFound();
        var module = await _db.Modules.FindAsync(moduleId);
        if (module != null && module.CourseId == courseId)
        {
            _db.Modules.Remove(module);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Manage", new { courseId });
    }

    // Add Content Item
    [HttpPost]
    public async Task<IActionResult> AddContent(int moduleId, int courseId, string title, string type, string? textContent, IFormFile? file)
    {
        if (!await CanManageCourseAsync(courseId) || string.IsNullOrWhiteSpace(title)) return NotFound();
        var module = await _db.Modules.FirstOrDefaultAsync(m => m.Id == moduleId && m.CourseId == courseId);
        if (module == null) return NotFound();
        string content = textContent ?? string.Empty;

        if (file != null && file.Length > 0)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploads, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            content = "/uploads/" + fileName;
        }

        var order = await _db.ContentItems.CountAsync(c => c.ModuleId == moduleId);
        _db.ContentItems.Add(new ContentItem
        {
            ModuleId = moduleId,
            Title = title,
            Type = type,
            Content = content,
            Order = order + 1
        });
        await _db.SaveChangesAsync();
        return RedirectToAction("Manage", new { courseId });
    }

    // Delete Content Item
    [HttpPost]
    public async Task<IActionResult> DeleteContent(int contentId, int courseId)
    {
        if (!await CanManageCourseAsync(courseId)) return NotFound();
        var item = await _db.ContentItems.FindAsync(contentId);
        if (item != null && await _db.Modules.AnyAsync(m => m.Id == item.ModuleId && m.CourseId == courseId))
        {
            _db.ContentItems.Remove(item);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Manage", new { courseId });
    }

    // Manage course content page
    public async Task<IActionResult> Manage(int courseId)
    {
        var course = await _db.Courses
            .Include(c => c.Modules)
                .ThenInclude(m => m.ContentItems)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null) return NotFound();
        if (!await CanManageCourseAsync(courseId)) return NotFound();

        ViewData["Title"] = "Manage Content";
        return View(course);
    }

    // View a single content item (students + instructors)
    [Authorize]
    public async Task<IActionResult> View(int id)
    {
        var item = await _db.ContentItems
            .Include(c => c.Module)
                .ThenInclude(m => m!.Course)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (item == null) return NotFound();

        ViewData["Title"] = item.Title;
        return View(item);
    }

    private async Task<bool> CanManageCourseAsync(int courseId)
    {
        if (User.IsInRole("Admin")) return await _db.Courses.AnyAsync(c => c.Id == courseId);
        var userId = _userManager.GetUserId(User);
        return await _db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == userId);
    }
}
