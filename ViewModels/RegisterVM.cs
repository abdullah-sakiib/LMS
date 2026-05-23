using System.ComponentModel.DataAnnotations;

namespace LMS.ViewModels;

public class RegisterVM
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [RegularExpression("^(Student|Instructor)$", ErrorMessage = "Select a valid role.")]
    public string Role { get; set; } = "Student";

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the terms to continue.")]
    public bool AcceptTerms { get; set; }
}
