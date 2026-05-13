using System.ComponentModel.DataAnnotations;

namespace BeDemo.Api.Models.DTOs;

/// <summary>Request body for <c>POST /api/me/push-token</c> (mobile registers FCM token after login).</summary>
public sealed class RegisterPushTokenRequestDto
{
    [Required]
    [MaxLength(512)]
    public string RegistrationToken { get; set; } = string.Empty;

    /// <summary><c>ios</c> or <c>android</c> (lowercase).</summary>
    [Required]
    [MaxLength(32)]
    public string Platform { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? InstallationId { get; set; }
}
