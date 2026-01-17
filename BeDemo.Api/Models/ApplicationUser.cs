using Microsoft.AspNetCore.Identity;

namespace BeDemo.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to UserProfile - one-to-one relationship
    /// </summary>
    public UserProfile? UserProfile { get; set; }
}
