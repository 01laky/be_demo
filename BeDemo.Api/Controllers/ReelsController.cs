using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReelsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRedisJobQueue _jobQueue;
    private readonly ILogger<ReelsController> _logger;

    public ReelsController(
        ApplicationDbContext context,
        IRedisJobQueue jobQueue,
        ILogger<ReelsController> logger)
    {
        _context = context;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    /// <summary>GET /api/reels?faceId= - Optional face filter: no ReelFaces = all faces; else only matching face.</summary>
    [HttpGet]
    public async Task<IActionResult> GetReels([FromQuery] int? faceId)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var query = _context.Reels.AsQueryable();

        if (faceId.HasValue)
        {
            query = query.Where(r =>
                !r.ReelFaces.Any() ||
                r.ReelFaces.Any(rf => rf.FaceId == faceId.Value));
        }

        var reels = await query
            .Include(r => r.Creator)
            .Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
            .Include(r => r.Likes)
            .Include(r => r.Comments)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                videoUrl = r.VideoUrl,
                creatorId = r.CreatorId,
                creatorName = (r.Creator.FirstName ?? "") + " " + (r.Creator.LastName ?? ""),
                faces = r.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
                likesCount = r.Likes.Count,
                commentsCount = r.Comments.Count,
                r.CreatedAt,
                r.UpdatedAt,
            })
            .ToListAsync();

        return Ok(reels);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReel(int id, [FromQuery] int? faceId)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var reel = await _context.Reels
            .Include(r => r.Creator)
            .Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
            .Include(r => r.Likes)
            .Include(r => r.Comments)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reel == null)
            return NotFound(new { error = "Reel not found" });

        if (!ReelVisibility.IsVisibleForFace(reel, faceId))
            return NotFound(new { error = "Reel not found" });

        return Ok(new
        {
            reel.Id,
            reel.Title,
            reel.Description,
            videoUrl = reel.VideoUrl,
            creatorId = reel.CreatorId,
            creatorName = (reel.Creator.FirstName ?? "") + " " + (reel.Creator.LastName ?? ""),
            faces = reel.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
            likesCount = reel.Likes.Count,
            commentsCount = reel.Comments.Count,
            isLikedByMe = reel.Likes.Any(l => l.UserId == UserId),
            reel.CreatedAt,
            reel.UpdatedAt,
        });
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetReelsByUser(string userId, [FromQuery] int? faceId)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var query = _context.Reels.Where(r => r.CreatorId == userId);

        if (faceId.HasValue)
        {
            query = query.Where(r =>
                !r.ReelFaces.Any() ||
                r.ReelFaces.Any(rf => rf.FaceId == faceId.Value));
        }

        var reels = await query
            .Include(r => r.Creator)
            .Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
            .Include(r => r.Likes)
            .Include(r => r.Comments)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Description,
                videoUrl = r.VideoUrl,
                creatorId = r.CreatorId,
                creatorName = (r.Creator.FirstName ?? "") + " " + (r.Creator.LastName ?? ""),
                faces = r.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
                likesCount = r.Likes.Count,
                commentsCount = r.Comments.Count,
                r.CreatedAt,
                r.UpdatedAt,
            })
            .ToListAsync();

        return Ok(reels);
    }

    [HttpPost]
    public async Task<IActionResult> CreateReel([FromBody] CreateReelDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { error = "Title is required" });

        if (string.IsNullOrWhiteSpace(dto.VideoUrl))
            return BadRequest(new { error = "VideoUrl is required" });

        var reel = new Reel
        {
            CreatorId = UserId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            VideoUrl = dto.VideoUrl.Trim(),
        };

        _context.Reels.Add(reel);
        await _context.SaveChangesAsync();

        if (dto.FaceIds is { Count: > 0 })
        {
            var validFaceIds = await _context.Faces
                .Where(f => dto.FaceIds.Contains(f.Id))
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var faceId in validFaceIds)
            {
                _context.ReelFaces.Add(new ReelFace { ReelId = reel.Id, FaceId = faceId });
            }

            await _context.SaveChangesAsync();
        }

        try
        {
            await _jobQueue.EnqueueAsync(
                "reel.postprocess",
                JsonSerializer.Serialize(new { reelId = reel.Id }),
                CancellationToken.None);
            await _jobQueue.ScheduleAsync(
                "reel.postprocess",
                JsonSerializer.Serialize(new { reelId = reel.Id, phase = "delayed_check" }),
                DateTime.UtcNow.AddHours(24),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue reel jobs for reel {ReelId}", reel.Id);
        }

        _logger.LogInformation("User {UserId} created reel {ReelId}", UserId, reel.Id);

        var created = await LoadReelDetailAsync(reel.Id, UserId);
        return CreatedAtAction(nameof(GetReel), new { id = reel.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateReel(int id, [FromBody] UpdateReelDto dto)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var reel = await _context.Reels
            .Include(r => r.ReelFaces)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reel == null)
            return NotFound(new { error = "Reel not found" });

        if (reel.CreatorId != UserId)
            return Forbid();

        if (dto.Title != null)
            reel.Title = dto.Title.Trim();
        if (dto.Description != null)
            reel.Description = dto.Description.Trim();
        if (dto.VideoUrl != null)
            reel.VideoUrl = dto.VideoUrl.Trim();

        reel.UpdatedAt = DateTime.UtcNow;

        if (dto.FaceIds != null)
        {
            _context.ReelFaces.RemoveRange(reel.ReelFaces);
            var validFaceIds = await _context.Faces
                .Where(f => dto.FaceIds.Contains(f.Id))
                .Select(f => f.Id)
                .ToListAsync();

            foreach (var faceId in validFaceIds)
            {
                _context.ReelFaces.Add(new ReelFace { ReelId = reel.Id, FaceId = faceId });
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("User {UserId} updated reel {ReelId}", UserId, reel.Id);

        var updated = await LoadReelDetailAsync(reel.Id, UserId);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteReel(int id)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var reel = await _context.Reels.FindAsync(id);
        if (reel == null)
            return NotFound(new { error = "Reel not found" });

        if (reel.CreatorId != UserId)
            return Forbid();

        _context.Reels.Remove(reel);
        await _context.SaveChangesAsync();
        _logger.LogInformation("User {UserId} deleted reel {ReelId}", UserId, id);
        return NoContent();
    }

    private async Task<object?> LoadReelDetailAsync(int reelId, string currentUserId)
    {
        var reel = await _context.Reels
            .Include(r => r.Creator)
            .Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
            .Include(r => r.Likes)
            .Include(r => r.Comments)
            .FirstOrDefaultAsync(r => r.Id == reelId);

        if (reel == null)
            return null;

        return new
        {
            reel.Id,
            reel.Title,
            reel.Description,
            videoUrl = reel.VideoUrl,
            creatorId = reel.CreatorId,
            creatorName = (reel.Creator.FirstName ?? "") + " " + (reel.Creator.LastName ?? ""),
            faces = reel.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
            likesCount = reel.Likes.Count,
            commentsCount = reel.Comments.Count,
            isLikedByMe = reel.Likes.Any(l => l.UserId == currentUserId),
            reel.CreatedAt,
            reel.UpdatedAt,
        };
    }

}

public class CreateReelDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string VideoUrl { get; set; } = string.Empty;
    public List<int>? FaceIds { get; set; }
}

public class UpdateReelDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? VideoUrl { get; set; }
    public List<int>? FaceIds { get; set; }
}
