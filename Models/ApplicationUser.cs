using Microsoft.AspNetCore.Identity;

namespace LMS.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePicture { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Bio { get; set; }
    public string? Department { get; set; }
    public string? University { get; set; }
    public string? Designation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
