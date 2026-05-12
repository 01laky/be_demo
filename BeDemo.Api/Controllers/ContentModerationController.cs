using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ContentModerationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAccessEvaluator _access;
    private readonly IContentModerationMetrics _metrics;

    public ContentModerationController(
        ApplicationDbContext context,
        IAccessEvaluator access,
        IContentModerationMetrics metrics)
    {
        _context = context;
        _access = access;
        _metrics = metrics;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    private bool CanModerate() => _access.IsGlobalSuperAdmin(User);

    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] ModeratedContentType? contentType,
        [FromQuery] ContentApprovalStatus? approvalStatus,
        [FromQuery] AiReviewStatus? aiReviewStatus,
        [FromQuery] int? faceId)
    {
        if (!CanModerate())
            return Forbid();

        var items = new List<ModerationItemDto>();

        if (contentType is null or ModeratedContentType.Album)
        {
            var albums = await _context.Albums
                .Include(a => a.Creator)
                .Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
                .Where(a => approvalStatus == null || a.ApprovalStatus == approvalStatus)
                .Where(a => aiReviewStatus == null || a.AiReviewStatus == aiReviewStatus)
                .Where(a => faceId == null || a.AlbumFaces.Any(af => af.FaceId == faceId))
                .Select(a => new ModerationItemDto(
                    ModeratedContentType.Album,
                    a.Id,
                    a.Title,
                    a.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
                    a.AlbumFaces.Select(af => af.Face.Title).FirstOrDefault() ?? string.Empty,
                    a.CreatorId,
                    (a.Creator.FirstName ?? "") + " " + (a.Creator.LastName ?? ""),
                    a.ApprovalStatus,
                    a.AiReviewStatus,
                    a.AiReviewDecision,
                    a.AiReviewConfidence,
                    a.AiReviewRiskLevel,
                    a.AiReviewFlagsJson,
                    a.AiReviewReason,
                    a.AiReviewUserMessage,
                    a.AiReviewModelVersion,
                    a.AiReviewTraceId,
                    a.SubmittedAtUtc,
                    a.HumanReviewedAtUtc,
                    a.HumanDecisionReason,
                    a.RemovedAtUtc,
                    a.RemovalReason,
                    a.CreatedAt))
                .ToListAsync();
            items.AddRange(albums);
        }

        if (contentType is null or ModeratedContentType.Blog)
        {
            var blogs = await _context.Blogs
                .Include(b => b.Creator)
                .Include(b => b.Face)
                .Where(b => approvalStatus == null || b.ApprovalStatus == approvalStatus)
                .Where(b => aiReviewStatus == null || b.AiReviewStatus == aiReviewStatus)
                .Where(b => faceId == null || b.FaceId == faceId)
                .Select(b => new ModerationItemDto(
                    ModeratedContentType.Blog,
                    b.Id,
                    b.Title,
                    b.FaceId,
                    b.Face.Title,
                    b.CreatorId,
                    (b.Creator.FirstName ?? "") + " " + (b.Creator.LastName ?? ""),
                    b.ApprovalStatus,
                    b.AiReviewStatus,
                    b.AiReviewDecision,
                    b.AiReviewConfidence,
                    b.AiReviewRiskLevel,
                    b.AiReviewFlagsJson,
                    b.AiReviewReason,
                    b.AiReviewUserMessage,
                    b.AiReviewModelVersion,
                    b.AiReviewTraceId,
                    b.SubmittedAtUtc,
                    b.HumanReviewedAtUtc,
                    b.HumanDecisionReason,
                    b.RemovedAtUtc,
                    b.RemovalReason,
                    b.CreatedAt))
                .ToListAsync();
            items.AddRange(blogs);
        }

        if (contentType is null or ModeratedContentType.Reel)
        {
            var reels = await _context.Reels
                .Include(r => r.Creator)
                .Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
                .Where(r => approvalStatus == null || r.ApprovalStatus == approvalStatus)
                .Where(r => aiReviewStatus == null || r.AiReviewStatus == aiReviewStatus)
                .Where(r => faceId == null || r.ReelFaces.Any(rf => rf.FaceId == faceId))
                .Select(r => new ModerationItemDto(
                    ModeratedContentType.Reel,
                    r.Id,
                    r.Title,
                    r.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
                    r.ReelFaces.Select(rf => rf.Face.Title).FirstOrDefault() ?? string.Empty,
                    r.CreatorId,
                    (r.Creator.FirstName ?? "") + " " + (r.Creator.LastName ?? ""),
                    r.ApprovalStatus,
                    r.AiReviewStatus,
                    r.AiReviewDecision,
                    r.AiReviewConfidence,
                    r.AiReviewRiskLevel,
                    r.AiReviewFlagsJson,
                    r.AiReviewReason,
                    r.AiReviewUserMessage,
                    r.AiReviewModelVersion,
                    r.AiReviewTraceId,
                    r.SubmittedAtUtc,
                    r.HumanReviewedAtUtc,
                    r.HumanDecisionReason,
                    r.RemovedAtUtc,
                    r.RemovalReason,
                    r.CreatedAt))
                .ToListAsync();
            items.AddRange(reels);
        }

        return Ok(items.OrderByDescending(i => i.SubmittedAtUtc ?? i.CreatedAt));
    }

    [HttpGet("{contentType}/{contentId:int}/events")]
    public async Task<IActionResult> GetEvents(ModeratedContentType contentType, int contentId)
    {
        if (!CanModerate())
            return Forbid();

        var events = await _context.ContentModerationEvents
            .Where(e => e.ContentType == contentType && e.ContentId == contentId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync();

        return Ok(events);
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        if (!CanModerate())
            return Forbid();

        return Ok(await _metrics.GetSnapshotAsync());
    }

    [HttpPost("{contentType}/{contentId:int}/approve")]
    public Task<IActionResult> Approve(
        ModeratedContentType contentType,
        int contentId,
        [FromBody] ModerationDecisionDto? decision) =>
        ApplyDecisionAsync(contentType, contentId, ContentApprovalStatus.Approved, decision);

    [HttpPost("{contentType}/{contentId:int}/reject")]
    public Task<IActionResult> Reject(
        ModeratedContentType contentType,
        int contentId,
        [FromBody] ModerationDecisionDto decision) =>
        ApplyDecisionAsync(contentType, contentId, ContentApprovalStatus.Rejected, decision);

    [HttpPost("{contentType}/{contentId:int}/remove")]
    public Task<IActionResult> Remove(
        ModeratedContentType contentType,
        int contentId,
        [FromBody] ModerationDecisionDto decision) =>
        ApplyDecisionAsync(contentType, contentId, ContentApprovalStatus.Removed, decision);

    private async Task<IActionResult> ApplyDecisionAsync(
        ModeratedContentType contentType,
        int contentId,
        ContentApprovalStatus targetStatus,
        ModerationDecisionDto? decision)
    {
        if (!CanModerate())
            return Forbid();
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();
        if (targetStatus is ContentApprovalStatus.Rejected or ContentApprovalStatus.Removed &&
            string.IsNullOrWhiteSpace(decision?.Reason))
        {
            return BadRequest(new { error = "Reason is required" });
        }

        var item = await LoadModeratedItemAsync(contentType, contentId);
        if (item == null)
            return NotFound(new { error = "Content not found" });

        if (item.ApprovalStatus == targetStatus)
            return Ok(item.ToResponse());

        if (targetStatus == ContentApprovalStatus.Approved &&
            item.AiReviewStatus == AiReviewStatus.RecommendedReject &&
            string.IsNullOrWhiteSpace(decision?.Reason))
        {
            return BadRequest(new { error = "Override reason is required when approving AI-recommended rejection" });
        }

        var oldApproval = item.ApprovalStatus;
        var oldAiStatus = item.AiReviewStatus;
        item.ApprovalStatus = targetStatus;
        item.HumanReviewedAtUtc = DateTime.UtcNow;
        item.HumanReviewedByUserId = UserId;
        item.HumanDecisionReason = decision?.Reason?.Trim();

        if (targetStatus == ContentApprovalStatus.Approved)
        {
            item.RemovedAtUtc = null;
            item.RemovedByUserId = null;
            item.RemovalReason = null;
        }
        else if (targetStatus == ContentApprovalStatus.Removed)
        {
            item.RemovedAtUtc = DateTime.UtcNow;
            item.RemovedByUserId = UserId;
            item.RemovalReason = decision?.Reason?.Trim();
        }

        _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
            contentType,
            contentId,
            item.FaceId,
            oldApproval,
            targetStatus,
            oldAiStatus,
            item.AiReviewStatus,
            ModerationActorType.SuperAdmin,
            UserId,
            decision?.Reason,
            decision?.UserMessage,
            item.AiReviewTraceId,
            item.AiReviewModelVersion));

        await _context.SaveChangesAsync();
        return Ok(item.ToResponse());
    }

    private async Task<ModeratedItemAdapter?> LoadModeratedItemAsync(ModeratedContentType contentType, int contentId)
    {
        switch (contentType)
        {
            case ModeratedContentType.Album:
                {
                    var album = await _context.Albums
                        .Include(a => a.AlbumFaces)
                        .FirstOrDefaultAsync(a => a.Id == contentId);
                    return album == null
                        ? null
                        : new ModeratedItemAdapter(
                            album.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
                            () => album.ApprovalStatus,
                            value => album.ApprovalStatus = value,
                            () => album.AiReviewStatus,
                            () => album.AiReviewTraceId,
                            () => album.AiReviewModelVersion,
                            value => album.HumanReviewedAtUtc = value,
                            value => album.HumanReviewedByUserId = value,
                            value => album.HumanDecisionReason = value,
                            value => album.RemovedAtUtc = value,
                            value => album.RemovedByUserId = value,
                            value => album.RemovalReason = value);
                }
            case ModeratedContentType.Blog:
                {
                    var blog = await _context.Blogs.FirstOrDefaultAsync(b => b.Id == contentId);
                    return blog == null
                        ? null
                        : new ModeratedItemAdapter(
                            blog.FaceId,
                            () => blog.ApprovalStatus,
                            value => blog.ApprovalStatus = value,
                            () => blog.AiReviewStatus,
                            () => blog.AiReviewTraceId,
                            () => blog.AiReviewModelVersion,
                            value => blog.HumanReviewedAtUtc = value,
                            value => blog.HumanReviewedByUserId = value,
                            value => blog.HumanDecisionReason = value,
                            value => blog.RemovedAtUtc = value,
                            value => blog.RemovedByUserId = value,
                            value => blog.RemovalReason = value);
                }
            case ModeratedContentType.Reel:
                {
                    var reel = await _context.Reels
                        .Include(r => r.ReelFaces)
                        .FirstOrDefaultAsync(r => r.Id == contentId);
                    return reel == null
                        ? null
                        : new ModeratedItemAdapter(
                            reel.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
                            () => reel.ApprovalStatus,
                            value => reel.ApprovalStatus = value,
                            () => reel.AiReviewStatus,
                            () => reel.AiReviewTraceId,
                            () => reel.AiReviewModelVersion,
                            value => reel.HumanReviewedAtUtc = value,
                            value => reel.HumanReviewedByUserId = value,
                            value => reel.HumanDecisionReason = value,
                            value => reel.RemovedAtUtc = value,
                            value => reel.RemovedByUserId = value,
                            value => reel.RemovalReason = value);
                }
            default:
                return null;
        }
    }

    private sealed class ModeratedItemAdapter
    {
        private readonly Func<ContentApprovalStatus> _getApprovalStatus;
        private readonly Action<ContentApprovalStatus> _setApprovalStatus;
        private readonly Func<AiReviewStatus> _getAiReviewStatus;
        private readonly Func<string?> _getAiTraceId;
        private readonly Func<string?> _getAiModelVersion;
        private readonly Action<DateTime?> _setHumanReviewedAtUtc;
        private readonly Action<string?> _setHumanReviewedByUserId;
        private readonly Action<string?> _setHumanDecisionReason;
        private readonly Action<DateTime?> _setRemovedAtUtc;
        private readonly Action<string?> _setRemovedByUserId;
        private readonly Action<string?> _setRemovalReason;

        public ModeratedItemAdapter(
            int faceId,
            Func<ContentApprovalStatus> getApprovalStatus,
            Action<ContentApprovalStatus> setApprovalStatus,
            Func<AiReviewStatus> getAiReviewStatus,
            Func<string?> getAiTraceId,
            Func<string?> getAiModelVersion,
            Action<DateTime?> setHumanReviewedAtUtc,
            Action<string?> setHumanReviewedByUserId,
            Action<string?> setHumanDecisionReason,
            Action<DateTime?> setRemovedAtUtc,
            Action<string?> setRemovedByUserId,
            Action<string?> setRemovalReason)
        {
            FaceId = faceId;
            _getApprovalStatus = getApprovalStatus;
            _setApprovalStatus = setApprovalStatus;
            _getAiReviewStatus = getAiReviewStatus;
            _getAiTraceId = getAiTraceId;
            _getAiModelVersion = getAiModelVersion;
            _setHumanReviewedAtUtc = setHumanReviewedAtUtc;
            _setHumanReviewedByUserId = setHumanReviewedByUserId;
            _setHumanDecisionReason = setHumanDecisionReason;
            _setRemovedAtUtc = setRemovedAtUtc;
            _setRemovedByUserId = setRemovedByUserId;
            _setRemovalReason = setRemovalReason;
        }

        public int FaceId { get; }

        public ContentApprovalStatus ApprovalStatus
        {
            get => _getApprovalStatus();
            set => _setApprovalStatus(value);
        }

        public AiReviewStatus AiReviewStatus => _getAiReviewStatus();

        public string? AiReviewTraceId => _getAiTraceId();

        public string? AiReviewModelVersion => _getAiModelVersion();

        public DateTime? HumanReviewedAtUtc
        {
            set => _setHumanReviewedAtUtc(value);
        }

        public string? HumanReviewedByUserId
        {
            set => _setHumanReviewedByUserId(value);
        }

        public string? HumanDecisionReason
        {
            set => _setHumanDecisionReason(value);
        }

        public DateTime? RemovedAtUtc
        {
            set => _setRemovedAtUtc(value);
        }

        public string? RemovedByUserId
        {
            set => _setRemovedByUserId(value);
        }

        public string? RemovalReason
        {
            set => _setRemovalReason(value);
        }

        public object ToResponse() => new
        {
            approvalStatus = ApprovalStatus.ToString(),
            aiReviewStatus = AiReviewStatus.ToString(),
        };
    }
}

public sealed record ModerationDecisionDto(string? Reason, string? UserMessage);

public sealed record ModerationItemDto(
    ModeratedContentType ContentType,
    int ContentId,
    string Title,
    int FaceId,
    string FaceTitle,
    string CreatorId,
    string CreatorName,
    ContentApprovalStatus ApprovalStatus,
    AiReviewStatus AiReviewStatus,
    AiReviewDecision AiReviewDecision,
    double? AiReviewConfidence,
    AiReviewRiskLevel AiReviewRiskLevel,
    string? AiReviewFlagsJson,
    string? AiReviewReason,
    string? AiReviewUserMessage,
    string? AiReviewModelVersion,
    string? AiReviewTraceId,
    DateTime? SubmittedAtUtc,
    DateTime? HumanReviewedAtUtc,
    string? HumanDecisionReason,
    DateTime? RemovedAtUtc,
    string? RemovalReason,
    DateTime CreatedAt);
