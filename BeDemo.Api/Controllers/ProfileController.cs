using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;
using BeDemo.Api.Data;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProfileController> _logger;

    private const string AvatarSubDir = "uploads/avatars";
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const int MaxFileSizeBytes = 30 * 1024 * 1024; // 30 MB (high‑quality / large photos)

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IWebHostEnvironment env,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _context = context;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/profile/me
    /// Returns current user profile. Optional ?faceId= for resolved avatar (local for that face, else global).
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile([FromQuery] int? faceId = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized();

        var profile = await _context.UserProfiles
            .Include(p => p.UserFaceProfiles)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
        }

        string? faceAvatarUrl = null;
        if (faceId.HasValue)
        {
            var faceProfile = await _context.UserFaceProfiles
                .FirstOrDefaultAsync(ufp => ufp.UserProfileId == profile.Id && ufp.FaceId == faceId.Value);
            faceAvatarUrl = faceProfile?.AvatarUrl;
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new
        {
            firstName = user.FirstName,
            lastName = user.LastName,
            email = user.Email,
            globalAvatarUrl = profile.AvatarUrl != null ? baseUrl + profile.AvatarUrl : (string?)null,
            faceAvatarUrl = faceAvatarUrl != null ? baseUrl + faceAvatarUrl : (string?)null,
        });
    }

    /// <summary>
    /// PUT /api/profile/me - update name
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest model)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Unauthorized();

        if (model.FirstName != null)
            user.FirstName = model.FirstName.Trim().Length > 0 ? model.FirstName.Trim() : null;
        if (model.LastName != null)
            user.LastName = model.LastName.Trim().Length > 0 ? model.LastName.Trim() : null;

        await _userManager.UpdateAsync(user);
        return Ok(new { message = "Profile updated" });
    }

    /// <summary>
    /// POST /api/profile/me/avatar - upload global avatar
    /// </summary>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadMyAvatar(IFormFile? file)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        var (path, error) = await SaveAvatarFile(file, userId, null);
        if (error != null)
            return BadRequest(new { error });

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
        }

        profile.AvatarUrl = path;
        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new { avatarUrl = baseUrl + path });
    }

    /// <summary>
    /// POST /api/profile/me/faces/{faceId}/avatar - upload face-specific avatar
    /// </summary>
    [HttpPost("me/faces/{faceId:int}/avatar")]
    public async Task<IActionResult> UploadMyFaceAvatar(int faceId, IFormFile? file)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
        }

        var faceExists = await _context.Faces.AnyAsync(f => f.Id == faceId);
        if (!faceExists)
            return NotFound(new { error = "Face not found" });

        var (path, error) = await SaveAvatarFile(file, userId, faceId);
        if (error != null)
            return BadRequest(new { error });

        var faceProfile = await _context.UserFaceProfiles
            .FirstOrDefaultAsync(ufp => ufp.UserProfileId == profile.Id && ufp.FaceId == faceId);

        if (faceProfile == null)
        {
            faceProfile = new UserFaceProfile
            {
                UserProfileId = profile.Id,
                FaceId = faceId,
                AvatarUrl = path,
            };
            _context.UserFaceProfiles.Add(faceProfile);
        }
        else
        {
            faceProfile.AvatarUrl = path;
            faceProfile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new { avatarUrl = baseUrl + path });
    }

    private async Task<(string? relativePath, string? error)> SaveAvatarFile(IFormFile file, string userId, int? faceId)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return (null, "Invalid file type. Allowed: " + string.Join(", ", AllowedExtensions));

        if (file.Length > MaxFileSizeBytes)
            return (null, "File too large. Max 30 MB.");

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, AvatarSubDir, userId);
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create avatar directory {Dir}", dir);
            return (null, "Server error creating upload directory");
        }

        var fileName = faceId.HasValue ? $"face_{faceId.Value}{ext}" : $"global{ext}";
        var fullPath = Path.Combine(dir, fileName);

        try
        {
            await using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save avatar file {Path}", fullPath);
            return (null, "Server error saving file");
        }

        var relativePath = "/" + AvatarSubDir.Replace('\\', '/') + "/" + userId + "/" + fileName;
        return (relativePath, null);
    }
}

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
