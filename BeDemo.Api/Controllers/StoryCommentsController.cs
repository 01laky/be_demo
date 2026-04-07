using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/stories/{storyId:int}/comments")]
[Authorize]
public class StoryCommentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StoryCommentsController> _logger;

    public StoryCommentsController(ApplicationDbContext context, ILogger<StoryCommentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetComments(int storyId, [FromQuery] int faceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, faceId, UserId, cancellationToken);
        if (story == null)
            return NotFound(new { error = "Story not found" });

        var comments = await _context.StoryComments
            .Where(c => c.StoryId == storyId)
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                userName = (c.User.FirstName ?? "") + " " + (c.User.LastName ?? ""),
                c.Content,
                c.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Ok(comments);
    }

    [HttpPost]
    public async Task<IActionResult> CreateComment(
        int storyId,
        [FromQuery] int faceId,
        [FromBody] CreateStoryCommentDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var story = await StoryInteractionGuard.GetLiveStoryForViewerAsync(_context, storyId, faceId, UserId, cancellationToken);
        if (story == null)
            return NotFound(new { error = "Story not found" });

        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { error = "Content is required" });

        var comment = new StoryComment
        {
            StoryId = storyId,
            UserId = UserId,
            Content = dto.Content.Trim(),
        };
        _context.StoryComments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} commented on story {StoryId}", UserId, storyId);
        return Ok(new { comment.Id });
    }
}

public class CreateStoryCommentDto
{
    public string Content { get; set; } = string.Empty;
}
