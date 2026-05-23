namespace LMS.Models;

public class Module
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
    public ICollection<ContentItem> ContentItems { get; set; } = new List<ContentItem>();
}