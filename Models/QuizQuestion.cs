using Microsoft.VisualBasic.FileIO;

namespace LMS.Models;

public class QuizQuestion
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Type { get; set; } = "MCQ"; // MCQ or TrueFalse
    public int Order { get; set; }
    public ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
}