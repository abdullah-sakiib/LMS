namespace LMS.Models;

public class QuizResponse
{
    public int Id { get; set; }
    public int QuizAttemptId { get; set; }
    public QuizAttempt? QuizAttempt { get; set; }
    public int QuizQuestionId { get; set; }
    public QuizQuestion? QuizQuestion { get; set; }
    public int? SelectedOptionId { get; set; }
    public QuizOption? SelectedOption { get; set; }
    public bool IsCorrect { get; set; }
    public double AwardedMarks { get; set; }
}
