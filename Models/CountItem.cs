namespace LMS.Models;

public class ContentItem
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public Module? Module { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "Lecture"; // Lecture, PDF, Video
    public string Content { get; set; } = string.Empty; // text, url, or filepath
    public int Order { get; set; }
}