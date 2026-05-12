using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

public interface IContentAiReviewService
{
    Task ProcessQueuedReviewAsync(string payloadJson, CancellationToken cancellationToken = default);
}

public interface IContentModerationMetrics
{
    Task<ContentModerationMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed record ContentModerationMetricsSnapshot(
    int PendingSubmissions,
    int AiQueuedJobs,
    int AiProcessingJobs,
    int AiFailedJobs,
    DateTime? OldestPendingSubmissionUtc);

public sealed class ContentAiReviewService : IContentAiReviewService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _context;
    private readonly IAiGrpcService _aiGrpcService;
    private readonly IRedisJobQueue _queue;
    private readonly ILogger<ContentAiReviewService> _logger;

    public ContentAiReviewService(
        ApplicationDbContext context,
        IAiGrpcService aiGrpcService,
        IRedisJobQueue queue,
        ILogger<ContentAiReviewService> logger)
    {
        _context = context;
        _aiGrpcService = aiGrpcService;
        _queue = queue;
        _logger = logger;
    }

    public async Task ProcessQueuedReviewAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        var payload = ParsePayload(payloadJson);
        if (payload == null)
        {
            _logger.LogWarning("Dropping invalid AI review payload: {Payload}", payloadJson);
            return;
        }

        var item = await LoadItemAsync(payload.ContentType, payload.ContentId, cancellationToken);
        if (item == null)
        {
            await MarkJobTerminalAsync(payload, AiReviewJobStatus.Failed, "Content no longer exists.", cancellationToken);
            return;
        }

        if (item.ModerationVersion != payload.ModerationVersion)
        {
            _logger.LogInformation(
                "Ignoring stale AI review job for {ContentType}:{ContentId}; payload v{PayloadVersion}, current v{CurrentVersion}",
                payload.ContentType,
                payload.ContentId,
                payload.ModerationVersion,
                item.ModerationVersion);
            await MarkJobTerminalAsync(payload, AiReviewJobStatus.Failed, "Stale moderation version.", cancellationToken);
            return;
        }

        if (item.ApprovalStatus != ContentApprovalStatus.PendingApproval)
        {
            await MarkJobTerminalAsync(payload, AiReviewJobStatus.Completed, "Content no longer pending.", cancellationToken);
            return;
        }

        var job = await GetOrCreateJobAsync(item, payload, cancellationToken);
        if (job.Status is AiReviewJobStatus.Completed or AiReviewJobStatus.NeedsHumanReview)
            return;
        if (job.Status == AiReviewJobStatus.Failed && job.Attempts >= job.MaxAttempts)
            return;

        var oldAiStatus = item.AiReviewStatus;
        job.Status = AiReviewJobStatus.Processing;
        job.Attempts += 1;
        job.StartedAtUtc = DateTime.UtcNow;
        job.LastError = null;
        item.AiReviewStatus = AiReviewStatus.InProgress;
        AddEvent(item, oldAiStatus, AiReviewStatus.InProgress, ModerationActorType.System, "AI review started.");
        await _context.SaveChangesAsync(cancellationToken);

        var result = await _aiGrpcService.ReviewContentAsync(item.ToAiRequest(), cancellationToken);
        if (result.Recommendation == null)
        {
            await HandleFailureAsync(item, job, result.Error ?? "AI review failed.", cancellationToken);
            return;
        }

        var validation = ContentModerationHelpers.ValidateRecommendation(result.Recommendation);
        ApplyRecommendation(item, result.Recommendation, validation);
        job.CompletedAtUtc = DateTime.UtcNow;
        job.LastError = validation.FallbackReason;
        job.Status = item.AiReviewStatus == AiReviewStatus.NeedsHumanReview
            ? AiReviewJobStatus.NeedsHumanReview
            : AiReviewJobStatus.Completed;

        AddEvent(
            item,
            AiReviewStatus.InProgress,
            item.AiReviewStatus,
            ModerationActorType.AI,
            validation.FallbackReason ?? result.Recommendation.Reason,
            result.Recommendation.UserMessage);

        _logger.LogInformation(
            "AI review completed for {ContentType}:{ContentId} v{ModerationVersion}: {AiReviewStatus} confidence={Confidence} risk={Risk}",
            item.ContentType,
            item.ContentId,
            item.ModerationVersion,
            item.AiReviewStatus,
            result.Recommendation.Confidence,
            result.Recommendation.RiskLevel);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static AiReviewPayload? ParsePayload(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("contentType", out var contentTypeEl) ||
                !Enum.TryParse<ModeratedContentType>(contentTypeEl.GetString(), true, out var contentType) ||
                !root.TryGetProperty("contentId", out var contentIdEl) ||
                !contentIdEl.TryGetInt32(out var contentId) ||
                !root.TryGetProperty("moderationVersion", out var versionEl) ||
                !versionEl.TryGetInt32(out var moderationVersion))
            {
                return null;
            }

            return new AiReviewPayload(contentType, contentId, moderationVersion);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ModeratedContentSnapshot?> LoadItemAsync(
        ModeratedContentType contentType,
        int contentId,
        CancellationToken cancellationToken)
    {
        switch (contentType)
        {
            case ModeratedContentType.Album:
                var album = await _context.Albums
                    .Include(a => a.AlbumFaces)
                    .FirstOrDefaultAsync(a => a.Id == contentId, cancellationToken);
                return album == null ? null : ModeratedContentSnapshot.ForAlbum(album);
            case ModeratedContentType.Blog:
                var blog = await _context.Blogs
                    .Include(b => b.Images)
                    .FirstOrDefaultAsync(b => b.Id == contentId, cancellationToken);
                return blog == null ? null : ModeratedContentSnapshot.ForBlog(blog);
            case ModeratedContentType.Reel:
                var reel = await _context.Reels
                    .Include(r => r.ReelFaces)
                    .FirstOrDefaultAsync(r => r.Id == contentId, cancellationToken);
                return reel == null ? null : ModeratedContentSnapshot.ForReel(reel);
            default:
                return null;
        }
    }

    private async Task<AiReviewJob> GetOrCreateJobAsync(
        ModeratedContentSnapshot item,
        AiReviewPayload payload,
        CancellationToken cancellationToken)
    {
        var job = await _context.AiReviewJobs
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(
                j => j.ContentType == payload.ContentType &&
                    j.ContentId == payload.ContentId &&
                    j.ModerationVersion == payload.ModerationVersion,
                cancellationToken);
        if (job != null)
            return job;

        job = new AiReviewJob
        {
            ContentType = payload.ContentType,
            ContentId = payload.ContentId,
            FaceId = item.FaceId,
            CreatedByUserId = item.CreatorId,
            Status = AiReviewJobStatus.Queued,
            ModerationVersion = payload.ModerationVersion,
            MaxAttempts = ContentModerationHelpers.DefaultMaxAttempts,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _context.AiReviewJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    private async Task HandleFailureAsync(
        ModeratedContentSnapshot item,
        AiReviewJob job,
        string error,
        CancellationToken cancellationToken)
    {
        var oldAiStatus = item.AiReviewStatus;
        job.LastError = ContentModerationHelpers.RedactForAudit(error);
        if (job.Attempts < job.MaxAttempts)
        {
            job.Status = AiReviewJobStatus.RetryScheduled;
            job.NextAttemptAtUtc = DateTime.UtcNow.Add(RetryDelay);
            item.AiReviewStatus = AiReviewStatus.Queued;
            await _queue.ScheduleAsync(
                ContentModerationHelpers.AiReviewJobType,
                ContentModerationHelpers.BuildAiReviewPayload(item.ContentType, item.ContentId, item.ModerationVersion),
                job.NextAttemptAtUtc.Value,
                cancellationToken);
            AddEvent(item, oldAiStatus, AiReviewStatus.Queued, ModerationActorType.System, error);
        }
        else
        {
            job.Status = AiReviewJobStatus.NeedsHumanReview;
            job.CompletedAtUtc = DateTime.UtcNow;
            item.AiReviewStatus = AiReviewStatus.NeedsHumanReview;
            item.AiReviewDecision = AiReviewDecision.NeedsHumanReview;
            item.AiReviewReason = "AI review failed after retries.";
            item.AiReviewUserMessage = "Your content needs manual review.";
            AddEvent(item, oldAiStatus, AiReviewStatus.NeedsHumanReview, ModerationActorType.System, error);
        }

        _logger.LogWarning(
            "AI review failed for {ContentType}:{ContentId} attempt {Attempt}/{MaxAttempts}: {Error}",
            item.ContentType,
            item.ContentId,
            job.Attempts,
            job.MaxAttempts,
            error);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private void ApplyRecommendation(
        ModeratedContentSnapshot item,
        AiReviewRecommendation recommendation,
        AiRecommendationValidationResult validation)
    {
        item.AiReviewDecision = validation.IsValid ? recommendation.Decision : AiReviewDecision.NeedsHumanReview;
        item.AiReviewConfidence = recommendation.Confidence;
        item.AiReviewRiskLevel = recommendation.RiskLevel;
        item.AiReviewFlagsJson = JsonSerializer.Serialize(recommendation.Flags);
        item.AiReviewReason = validation.FallbackReason ?? recommendation.Reason;
        item.AiReviewUserMessage = recommendation.UserMessage;
        item.AiReviewModelVersion = recommendation.ModelVersion;
        item.AiReviewTraceId = recommendation.TraceId;
        item.AiReviewedAtUtc = DateTime.UtcNow;
        item.AiReviewStatus = validation.IsValid
            ? recommendation.Decision switch
            {
                AiReviewDecision.Approve => AiReviewStatus.RecommendedApprove,
                AiReviewDecision.Reject => AiReviewStatus.RecommendedReject,
                _ => AiReviewStatus.NeedsHumanReview,
            }
            : AiReviewStatus.NeedsHumanReview;
    }

    private async Task MarkJobTerminalAsync(
        AiReviewPayload payload,
        AiReviewJobStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        var job = await _context.AiReviewJobs
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(
                j => j.ContentType == payload.ContentType &&
                    j.ContentId == payload.ContentId &&
                    j.ModerationVersion == payload.ModerationVersion,
                cancellationToken);
        if (job == null)
            return;

        job.Status = status;
        job.CompletedAtUtc = DateTime.UtcNow;
        job.LastError = ContentModerationHelpers.RedactForAudit(reason);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private void AddEvent(
        ModeratedContentSnapshot item,
        AiReviewStatus? oldAiReviewStatus,
        AiReviewStatus? newAiReviewStatus,
        ModerationActorType actorType,
        string? reason,
        string? userMessage = null)
    {
        _context.ContentModerationEvents.Add(ContentModerationHelpers.BuildEvent(
            item.ContentType,
            item.ContentId,
            item.FaceId,
            item.ApprovalStatus,
            item.ApprovalStatus,
            oldAiReviewStatus,
            newAiReviewStatus,
            actorType,
            null,
            reason,
            userMessage,
            item.AiReviewTraceId,
            item.AiReviewModelVersion));
    }

    private sealed record AiReviewPayload(
        ModeratedContentType ContentType,
        int ContentId,
        int ModerationVersion);

    private sealed class ModeratedContentSnapshot
    {
        private readonly Func<ContentApprovalStatus> _getApprovalStatus;
        private readonly Func<AiReviewStatus> _getAiReviewStatus;
        private readonly Action<AiReviewStatus> _setAiReviewStatus;
        private readonly Action<AiReviewDecision> _setAiReviewDecision;
        private readonly Action<double?> _setAiReviewConfidence;
        private readonly Action<AiReviewRiskLevel> _setAiReviewRiskLevel;
        private readonly Action<string?> _setAiReviewFlagsJson;
        private readonly Action<string?> _setAiReviewReason;
        private readonly Action<string?> _setAiReviewUserMessage;
        private readonly Action<string?> _setAiReviewModelVersion;
        private readonly Action<string?> _setAiReviewTraceId;
        private readonly Action<DateTime?> _setAiReviewedAtUtc;
        private string? _aiReviewModelVersion;
        private string? _aiReviewTraceId;

        private ModeratedContentSnapshot(
            ModeratedContentType contentType,
            int contentId,
            int faceId,
            string creatorId,
            string title,
            string body,
            string? mediaUrl,
            int moderationVersion,
            Func<ContentApprovalStatus> getApprovalStatus,
            Func<AiReviewStatus> getAiReviewStatus,
            Action<AiReviewStatus> setAiReviewStatus,
            Action<AiReviewDecision> setAiReviewDecision,
            Action<double?> setAiReviewConfidence,
            Action<AiReviewRiskLevel> setAiReviewRiskLevel,
            Action<string?> setAiReviewFlagsJson,
            Action<string?> setAiReviewReason,
            Action<string?> setAiReviewUserMessage,
            Action<string?> setAiReviewModelVersion,
            Action<string?> setAiReviewTraceId,
            Action<DateTime?> setAiReviewedAtUtc)
        {
            ContentType = contentType;
            ContentId = contentId;
            FaceId = faceId;
            CreatorId = creatorId;
            Title = title;
            Body = body;
            MediaUrl = mediaUrl;
            ModerationVersion = moderationVersion;
            _getApprovalStatus = getApprovalStatus;
            _getAiReviewStatus = getAiReviewStatus;
            _setAiReviewStatus = setAiReviewStatus;
            _setAiReviewDecision = setAiReviewDecision;
            _setAiReviewConfidence = setAiReviewConfidence;
            _setAiReviewRiskLevel = setAiReviewRiskLevel;
            _setAiReviewFlagsJson = setAiReviewFlagsJson;
            _setAiReviewReason = setAiReviewReason;
            _setAiReviewUserMessage = setAiReviewUserMessage;
            _setAiReviewModelVersion = setAiReviewModelVersion;
            _setAiReviewTraceId = setAiReviewTraceId;
            _setAiReviewedAtUtc = setAiReviewedAtUtc;
        }

        public static ModeratedContentSnapshot ForAlbum(Album album) => new(
            ModeratedContentType.Album,
            album.Id,
            album.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
            album.CreatorId,
            album.Title,
            album.Description ?? string.Empty,
            null,
            album.ModerationVersion,
            () => album.ApprovalStatus,
            () => album.AiReviewStatus,
            value => album.AiReviewStatus = value,
            value => album.AiReviewDecision = value,
            value => album.AiReviewConfidence = value,
            value => album.AiReviewRiskLevel = value,
            value => album.AiReviewFlagsJson = value,
            value => album.AiReviewReason = value,
            value => album.AiReviewUserMessage = value,
            value => album.AiReviewModelVersion = value,
            value => album.AiReviewTraceId = value,
            value => album.AiReviewedAtUtc = value);

        public static ModeratedContentSnapshot ForBlog(Blog blog) => new(
            ModeratedContentType.Blog,
            blog.Id,
            blog.FaceId,
            blog.CreatorId,
            blog.Title,
            blog.Content,
            blog.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault(),
            blog.ModerationVersion,
            () => blog.ApprovalStatus,
            () => blog.AiReviewStatus,
            value => blog.AiReviewStatus = value,
            value => blog.AiReviewDecision = value,
            value => blog.AiReviewConfidence = value,
            value => blog.AiReviewRiskLevel = value,
            value => blog.AiReviewFlagsJson = value,
            value => blog.AiReviewReason = value,
            value => blog.AiReviewUserMessage = value,
            value => blog.AiReviewModelVersion = value,
            value => blog.AiReviewTraceId = value,
            value => blog.AiReviewedAtUtc = value);

        public static ModeratedContentSnapshot ForReel(Reel reel) => new(
            ModeratedContentType.Reel,
            reel.Id,
            reel.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
            reel.CreatorId,
            reel.Title,
            reel.Description ?? string.Empty,
            reel.VideoUrl,
            reel.ModerationVersion,
            () => reel.ApprovalStatus,
            () => reel.AiReviewStatus,
            value => reel.AiReviewStatus = value,
            value => reel.AiReviewDecision = value,
            value => reel.AiReviewConfidence = value,
            value => reel.AiReviewRiskLevel = value,
            value => reel.AiReviewFlagsJson = value,
            value => reel.AiReviewReason = value,
            value => reel.AiReviewUserMessage = value,
            value => reel.AiReviewModelVersion = value,
            value => reel.AiReviewTraceId = value,
            value => reel.AiReviewedAtUtc = value);

        public ModeratedContentType ContentType { get; }
        public int ContentId { get; }
        public int FaceId { get; }
        public string CreatorId { get; }
        public string Title { get; }
        public string Body { get; }
        public string? MediaUrl { get; }
        public int ModerationVersion { get; }

        public ContentApprovalStatus ApprovalStatus => _getApprovalStatus();

        public AiReviewStatus AiReviewStatus
        {
            get => _getAiReviewStatus();
            set => _setAiReviewStatus(value);
        }

        public AiReviewDecision AiReviewDecision
        {
            set => _setAiReviewDecision(value);
        }

        public double? AiReviewConfidence
        {
            set => _setAiReviewConfidence(value);
        }

        public AiReviewRiskLevel AiReviewRiskLevel
        {
            set => _setAiReviewRiskLevel(value);
        }

        public string? AiReviewFlagsJson
        {
            set => _setAiReviewFlagsJson(value);
        }

        public string? AiReviewReason
        {
            set => _setAiReviewReason(value);
        }

        public string? AiReviewUserMessage
        {
            set => _setAiReviewUserMessage(value);
        }

        public string? AiReviewModelVersion
        {
            get => _aiReviewModelVersion;
            set
            {
                _aiReviewModelVersion = value;
                _setAiReviewModelVersion(value);
            }
        }

        public string? AiReviewTraceId
        {
            get => _aiReviewTraceId;
            set
            {
                _aiReviewTraceId = value;
                _setAiReviewTraceId(value);
            }
        }

        public DateTime? AiReviewedAtUtc
        {
            set => _setAiReviewedAtUtc(value);
        }

        public AiContentReviewRequest ToAiRequest() => new(
            ContentType,
            ContentId,
            ModerationVersion,
            FaceId,
            Title,
            Body,
            MediaUrl,
            CreatorId);
    }
}

public sealed class ContentModerationMetrics : IContentModerationMetrics
{
    private readonly ApplicationDbContext _context;

    public ContentModerationMetrics(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContentModerationMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var pendingAlbums = _context.Albums.Where(a => a.ApprovalStatus == ContentApprovalStatus.PendingApproval);
        var pendingBlogs = _context.Blogs.Where(b => b.ApprovalStatus == ContentApprovalStatus.PendingApproval);
        var pendingReels = _context.Reels.Where(r => r.ApprovalStatus == ContentApprovalStatus.PendingApproval);
        var pendingCount =
            await pendingAlbums.CountAsync(cancellationToken) +
            await pendingBlogs.CountAsync(cancellationToken) +
            await pendingReels.CountAsync(cancellationToken);
        var oldestDates = new[]
        {
            await pendingAlbums.MinAsync(a => (DateTime?)a.SubmittedAtUtc, cancellationToken),
            await pendingBlogs.MinAsync(b => (DateTime?)b.SubmittedAtUtc, cancellationToken),
            await pendingReels.MinAsync(r => (DateTime?)r.SubmittedAtUtc, cancellationToken),
        };

        return new ContentModerationMetricsSnapshot(
            pendingCount,
            await _context.AiReviewJobs.CountAsync(j => j.Status == AiReviewJobStatus.Queued || j.Status == AiReviewJobStatus.RetryScheduled, cancellationToken),
            await _context.AiReviewJobs.CountAsync(j => j.Status == AiReviewJobStatus.Processing, cancellationToken),
            await _context.AiReviewJobs.CountAsync(j => j.Status == AiReviewJobStatus.Failed, cancellationToken),
            oldestDates.Where(d => d.HasValue).DefaultIfEmpty().Min());
    }
}
