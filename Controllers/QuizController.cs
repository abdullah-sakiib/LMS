using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Controllers;

[Authorize]
public class QuizController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public QuizController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int? courseId)
    {
        var userId = _userManager.GetUserId(User)!;
        var isStudent = User.IsInRole("Student");
        IQueryable<Quiz> query = _db.Quizzes
            .Include(quiz => quiz.Course).ThenInclude(course => course!.Enrollments).ThenInclude(enrollment => enrollment.Student)
            .Include(quiz => quiz.Questions)
            .Include(quiz => quiz.Attempts).ThenInclude(attempt => attempt.Student);

        if (isStudent)
        {
            query = query.Where(quiz => quiz.IsPublished && quiz.Course != null &&
                quiz.Course.Enrollments.Any(enrollment => enrollment.StudentId == userId && enrollment.Status == "Approved"));
        }
        else if (User.IsInRole("Instructor"))
        {
            query = query.Where(quiz => quiz.Course != null && quiz.Course.InstructorId == userId);
        }

        if (courseId.HasValue)
        {
            if (!isStudent && !await CanManageCourseAsync(courseId.Value)) return NotFound();
            query = query.Where(quiz => quiz.CourseId == courseId.Value);
        }

        var quizzes = await query.OrderByDescending(quiz => quiz.CreatedAt).ToListAsync();
        var cards = quizzes.Select(quiz =>
        {
            var submitted = quiz.Attempts.Where(attempt => attempt.SubmittedAt.HasValue).ToList();
            var enrolled = quiz.Course?.Enrollments.Where(enrollment => enrollment.Status == "Approved" && enrollment.Student != null).ToList() ?? new();
            var submittedStudentIds = submitted.Select(attempt => attempt.StudentId).ToHashSet();
            return new QuizCardVM
            {
                Quiz = quiz,
                MyAttempt = submitted.FirstOrDefault(attempt => attempt.StudentId == userId),
                QuestionCount = quiz.Questions.Count,
                EligibleStudents = enrolled.Count,
                AttemptCount = submitted.Count,
                AverageScorePercent = submitted.Count == 0 ? 0 : Math.Round(submitted.Average(attempt => attempt.Score / Math.Max(1, quiz.TotalMarks) * 100)),
                ParticipationPercent = enrolled.Count == 0 ? 0 : Math.Round(submitted.Count * 100d / enrolled.Count),
                AttemptedStudents = enrolled.Where(enrollment => submittedStudentIds.Contains(enrollment.StudentId)).Select(enrollment => enrollment.Student!).ToList(),
                NotAttemptedStudents = enrolled.Where(enrollment => !submittedStudentIds.Contains(enrollment.StudentId)).Select(enrollment => enrollment.Student!).ToList()
            };
        }).ToList();

        var relevantAttempts = isStudent
            ? cards.Where(card => card.MyAttempt != null).Select(card => new { card.MyAttempt!.Score, card.Quiz.TotalMarks }).ToList()
            : cards.SelectMany(card => card.Quiz.Attempts.Where(attempt => attempt.SubmittedAt.HasValue)
                .Select(attempt => new { attempt.Score, card.Quiz.TotalMarks })).ToList();
        var distinctStudents = cards.SelectMany(card => card.AttemptedStudents).Select(student => student.Id).Distinct().Count();
        var distinctEnrolled = cards.SelectMany(card => card.Quiz.Course!.Enrollments
            .Where(enrollment => enrollment.Status == "Approved").Select(enrollment => enrollment.StudentId)).Distinct().Count();

        var viewModel = new QuizWorkspaceVM
        {
            IsStudent = isStudent,
            Cards = cards,
            Total = cards.Count,
            Completed = isStudent ? cards.Count(card => card.MyAttempt != null) : distinctStudents,
            Pending = isStudent ? cards.Count(card => card.MyAttempt == null) : Math.Max(0, distinctEnrolled - distinctStudents),
            AveragePercent = relevantAttempts.Count == 0 ? 0 : Math.Round(relevantAttempts.Average(attempt => attempt.Score / Math.Max(1, attempt.TotalMarks) * 100)),
            TotalAttempts = cards.Sum(card => card.AttemptCount),
            TotalStudents = distinctEnrolled,
            ParticipationPercent = distinctEnrolled == 0 ? 0 : Math.Round(distinctStudents * 100d / distinctEnrolled)
        };
        if (!isStudent)
        {
            IQueryable<Course> courses = _db.Courses.Where(course => course.Status == "Published");
            if (User.IsInRole("Instructor")) courses = courses.Where(course => course.InstructorId == userId);
            viewModel.InstructorCourses = await courses.OrderBy(course => course.Title).ToListAsync();
        }

        ViewData["Title"] = "Quizzes";
        ViewData["PageTitle"] = "Quizzes";
        ViewData["PageSubtitle"] = isStudent ? "Test your knowledge" : "Manage quizzes and review attempts";
        return View(viewModel);
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> CreateQuick(string title, int courseId, int totalMarks = 100, int timerMinutes = 30)
    {
        if (!await CanManageCourseAsync(courseId)) return NotFound();
        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Quiz title is required.";
            return RedirectToAction(nameof(Index));
        }
        var quiz = new Quiz
        {
            CourseId = courseId,
            Title = title.Trim(),
            TotalMarks = Math.Max(1, totalMarks),
            TimerMinutes = Math.Max(1, timerMinutes),
            IsPublished = false
        };
        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Quiz '{quiz.Title}' created. Add questions to publish it for students.";
        return RedirectToAction(nameof(Edit), new { id = quiz.Id });
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create(int courseId)
    {
        if (!await CanManageCourseAsync(courseId)) return NotFound();
        return View(new QuizVM { CourseId = courseId });
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Create([FromBody] QuizVM vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Title) || !vm.Questions.Any()) return BadRequest("Title and at least one question are required.");
        if (!await CanManageCourseAsync(vm.CourseId)) return NotFound();
        var quiz = new Quiz
        {
            CourseId = vm.CourseId,
            Title = vm.Title.Trim(),
            TotalMarks = Math.Max(1, vm.TotalMarks),
            TimerMinutes = Math.Max(1, vm.TimerMinutes)
        };
        AddQuestions(quiz, vm.Questions);
        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync();
        return Ok(new { courseId = vm.CourseId });
    }

    [Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var quiz = await _db.Quizzes.Include(item => item.Course)
            .Include(item => item.Questions).ThenInclude(question => question.Options)
            .Include(item => item.Attempts).FirstOrDefaultAsync(item => item.Id == id);
        if (quiz == null || !await CanManageCourseAsync(quiz.CourseId)) return NotFound();
        return View(quiz);
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Edit(int id, [FromBody] QuizVM vm)
    {
        var quiz = await _db.Quizzes.Include(item => item.Questions).ThenInclude(question => question.Options)
            .Include(item => item.Attempts).FirstOrDefaultAsync(item => item.Id == id);
        if (quiz == null || !await CanManageCourseAsync(quiz.CourseId)) return NotFound();
        if (quiz.Attempts.Any()) return BadRequest("A quiz with attempts cannot be edited.");
        if (string.IsNullOrWhiteSpace(vm.Title) || !vm.Questions.Any()) return BadRequest("Title and at least one question are required.");
        quiz.Title = vm.Title.Trim();
        quiz.TotalMarks = Math.Max(1, vm.TotalMarks);
        quiz.TimerMinutes = Math.Max(1, vm.TimerMinutes);
        quiz.IsPublished = true;
        _db.QuizQuestions.RemoveRange(quiz.Questions);
        quiz.Questions.Clear();
        AddQuestions(quiz, vm.Questions);
        await _db.SaveChangesAsync();
        return Ok(new { courseId = quiz.CourseId });
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Take(int id)
    {
        var quiz = await _db.Quizzes.Include(item => item.Course)
            .Include(item => item.Questions).ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(item => item.Id == id && item.IsPublished);
        if (quiz == null) return NotFound();
        var userId = _userManager.GetUserId(User)!;
        if (!await _db.Enrollments.AnyAsync(enrollment => enrollment.CourseId == quiz.CourseId &&
            enrollment.StudentId == userId && enrollment.Status == "Approved"))
        {
            TempData["Error"] = "You can only take quizzes for enrolled courses.";
            return RedirectToAction(nameof(Index));
        }
        if (!quiz.Questions.Any())
        {
            TempData["Error"] = "This quiz has no questions yet.";
            return RedirectToAction(nameof(Index));
        }
        var attempt = await _db.QuizAttempts.Include(item => item.Responses).ThenInclude(response => response.SelectedOption)
            .FirstOrDefaultAsync(item => item.QuizId == id && item.StudentId == userId);
        return View(new QuizTakeVM
        {
            Quiz = quiz,
            Attempt = attempt,
            Responses = attempt?.Responses.ToDictionary(response => response.QuizQuestionId) ?? new()
        });
    }

    [HttpPost, Authorize(Roles = "Student")]
    public async Task<IActionResult> Submit([FromBody] QuizSubmitVM vm)
    {
        var quiz = await _db.Quizzes.Include(item => item.Questions).ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(item => item.Id == vm.QuizId && item.IsPublished);
        if (quiz == null) return NotFound();
        var userId = _userManager.GetUserId(User)!;
        if (!await _db.Enrollments.AnyAsync(enrollment => enrollment.CourseId == quiz.CourseId &&
            enrollment.StudentId == userId && enrollment.Status == "Approved")) return Forbid();
        if (await _db.QuizAttempts.AnyAsync(attempt => attempt.QuizId == quiz.Id &&
            attempt.StudentId == userId && attempt.SubmittedAt.HasValue)) return Conflict("This quiz was already submitted.");

        var pointsPerQuestion = quiz.Questions.Count == 0 ? 0 : quiz.TotalMarks / (double)quiz.Questions.Count;
        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            StudentId = userId,
            TotalQuestions = quiz.Questions.Count,
            SubmittedAt = DateTime.UtcNow
        };
        foreach (var question in quiz.Questions.OrderBy(item => item.Order))
        {
            vm.Answers.TryGetValue(question.Id, out var selectedOptionId);
            var option = question.Options.FirstOrDefault(item => item.Id == selectedOptionId);
            var correct = option?.IsCorrect == true;
            var awarded = correct ? pointsPerQuestion : 0;
            attempt.Score += awarded;
            attempt.Responses.Add(new QuizResponse
            {
                QuizQuestionId = question.Id,
                SelectedOptionId = option?.Id,
                IsCorrect = correct,
                AwardedMarks = Math.Round(awarded, 2)
            });
        }
        attempt.Score = Math.Round(attempt.Score, 2);
        _db.QuizAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        return Ok(new { score = attempt.Score, totalMarks = quiz.TotalMarks, correct = attempt.Responses.Count(response => response.IsCorrect), total = quiz.Questions.Count });
    }

    public async Task<IActionResult> Results(int id)
    {
        var quiz = await _db.Quizzes.Include(item => item.Course)
            .Include(item => item.Attempts).ThenInclude(attempt => attempt.Student)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (quiz == null) return NotFound();
        if (User.IsInRole("Student"))
        {
            var userId = _userManager.GetUserId(User);
            if (!await _db.Enrollments.AnyAsync(enrollment => enrollment.CourseId == quiz.CourseId &&
                enrollment.StudentId == userId && enrollment.Status == "Approved")) return NotFound();
        }
        else if (!await CanManageCourseAsync(quiz.CourseId)) return NotFound();
        ViewBag.IsInstructor = !User.IsInRole("Student");
        ViewBag.MyAttempt = quiz.Attempts.FirstOrDefault(attempt => attempt.StudentId == _userManager.GetUserId(User));
        return View(quiz);
    }

    [HttpPost, Authorize(Roles = "Instructor,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var quiz = await _db.Quizzes.FindAsync(id);
        if (quiz == null || !await CanManageCourseAsync(quiz.CourseId)) return NotFound();
        _db.Quizzes.Remove(quiz);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private static void AddQuestions(Quiz quiz, IEnumerable<QuizQuestionVM> questions)
    {
        var order = 1;
        foreach (var source in questions)
        {
            var question = new QuizQuestion { QuestionText = source.QuestionText.Trim(), Type = source.Type == "TrueFalse" ? "TrueFalse" : "MCQ", Order = order++ };
            var options = question.Type == "TrueFalse" ? new[] { "True", "False" } : source.Options.ToArray();
            for (var index = 0; index < options.Length; index++)
                question.Options.Add(new QuizOption { OptionText = options[index].Trim(), IsCorrect = index == source.CorrectOptionIndex });
            quiz.Questions.Add(question);
        }
    }

    private async Task<bool> CanManageCourseAsync(int courseId)
    {
        if (User.IsInRole("Admin")) return await _db.Courses.AnyAsync(course => course.Id == courseId);
        var userId = _userManager.GetUserId(User);
        return await _db.Courses.AnyAsync(course => course.Id == courseId && course.InstructorId == userId);
    }
}
