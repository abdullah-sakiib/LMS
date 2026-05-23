namespace LMS.Models;

public class QuizAttempt
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser? Student { get; set; }
    public double Score { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public ICollection<QuizResponse> Responses { get; set; } = new List<QuizResponse>();
}
