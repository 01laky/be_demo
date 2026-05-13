using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Platform dashboard statistics for the admin SPA: consolidated counts (<c>GET /api/Stats</c>) and optional
/// UTC histograms (<c>GET /api/Stats/timeseries</c>). Both require the same operator bar as other admin-wide APIs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StatsController : ControllerBase
{
    private const int MaxTimeseriesRangeDays = 366;

    private readonly ApplicationDbContext _context;
    private readonly IAccessEvaluator _access;
    private readonly ILogger<StatsController> _logger;

    public StatsController(
        ApplicationDbContext context,
        IAccessEvaluator access,
        ILogger<StatsController> logger)
    {
        _context = context;
        _access = access;
        _logger = logger;
    }

    /// <summary>
    /// Returns the full <see cref="AdminDashboardSummaryDto"/> for authorized platform operators.
    /// </summary>
    /// <remarks>
    /// Authorization: <see cref="IAccessEvaluator.CanManageAllFaces"/> (admin face HTTP scope + global Admin/SuperAdmin role).
    /// Implementation uses sequential <c>CountAsync</c> calls for clarity and compatibility with the in-memory test provider;
    /// optimize with batched SQL or <c>IDbContextFactory</c> if this becomes hot in production traces.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<AdminDashboardSummaryDto>> GetStats(CancellationToken cancellationToken)
    {
        if (!_access.CanManageAllFaces(User))
        {
            _logger.LogDebug("Stats summary denied for user lacking CanManageAllFaces.");
            return Forbid();
        }

        var dto = new AdminDashboardSummaryDto
        {
            UsersCount = await _context.Users.AsNoTracking().CountAsync(cancellationToken),
            FriendRequestsCount = await _context.FriendRequests.AsNoTracking()
                .CountAsync(r => r.Status == FriendRequestStatus.Pending, cancellationToken),
            MessagesCount = await _context.Messages.AsNoTracking().CountAsync(cancellationToken),

            FacesCount = await _context.Faces.AsNoTracking().CountAsync(cancellationToken),
            PagesCount = await _context.Pages.AsNoTracking().CountAsync(cancellationToken),
            PageComponentsCount = await _context.PageComponents.AsNoTracking().CountAsync(cancellationToken),
            PageRouteTranslationsCount = await _context.PageRouteTranslations.AsNoTracking().CountAsync(cancellationToken),

            FriendshipsCount = await _context.Friendships.AsNoTracking().CountAsync(cancellationToken),
            FriendRequestsAcceptedCount = await _context.FriendRequests.AsNoTracking()
                .CountAsync(r => r.Status == FriendRequestStatus.Accepted, cancellationToken),
            FriendRequestsRejectedCount = await _context.FriendRequests.AsNoTracking()
                .CountAsync(r => r.Status == FriendRequestStatus.Rejected, cancellationToken),
            UserFollowsCount = await _context.UserFollows.AsNoTracking().CountAsync(cancellationToken),
            UserBlocksCount = await _context.UserBlocks.AsNoTracking().CountAsync(cancellationToken),

            MessagesPendingRequestCount = await _context.Messages.AsNoTracking()
                .CountAsync(m => m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending, cancellationToken),

            NotificationsCount = await _context.Notifications.AsNoTracking().CountAsync(cancellationToken),

            AlbumsCount = await _context.Albums.AsNoTracking().CountAsync(cancellationToken),
            BlogsCount = await _context.Blogs.AsNoTracking().CountAsync(cancellationToken),
            ReelsCount = await _context.Reels.AsNoTracking().CountAsync(cancellationToken),
            StoriesCount = await _context.Stories.AsNoTracking().CountAsync(cancellationToken),
            StoryViewsCount = await _context.StoryViews.AsNoTracking().CountAsync(cancellationToken),

            FaceChatRoomsCount = await _context.FaceChatRooms.AsNoTracking().CountAsync(cancellationToken),
            FaceChatRoomMembersCount = await _context.FaceChatRoomMembers.AsNoTracking().CountAsync(cancellationToken),
            FaceChatRoomMessagesCount = await _context.FaceChatRoomMessages.AsNoTracking().CountAsync(cancellationToken),
            FaceChatRoomJoinRequestsPendingCount = await _context.FaceChatRoomJoinRequests.AsNoTracking()
                .CountAsync(j => j.Status == FaceChatRoomJoinRequestStatus.Pending, cancellationToken),

            FaceWallTicketsCount = await _context.FaceWallTickets.AsNoTracking().CountAsync(cancellationToken),
            FaceWallTicketsByStatus = await BuildFaceWallTicketStatusCountsAsync(cancellationToken),
            FaceWallTicketCommentsCount = await _context.FaceWallTicketComments.AsNoTracking().CountAsync(cancellationToken),
            FaceWallTicketLikesCount = await _context.FaceWallTicketLikes.AsNoTracking().CountAsync(cancellationToken),

            UserFaceProfilesCount = await _context.UserFaceProfiles.AsNoTracking().CountAsync(cancellationToken),
            UserFaceProfileLikesCount = await _context.UserFaceProfileLikes.AsNoTracking().CountAsync(cancellationToken),
            UserFaceProfileCommentsCount = await _context.UserFaceProfileComments.AsNoTracking().CountAsync(cancellationToken),
            UserFaceProfileReviewsCount = await _context.UserFaceProfileReviews.AsNoTracking().CountAsync(cancellationToken),

            AlbumCommentsCount = await _context.AlbumComments.AsNoTracking().CountAsync(cancellationToken),
            BlogCommentsCount = await _context.BlogComments.AsNoTracking().CountAsync(cancellationToken),
            ReelCommentsCount = await _context.ReelComments.AsNoTracking().CountAsync(cancellationToken),
            StoryCommentsCount = await _context.StoryComments.AsNoTracking().CountAsync(cancellationToken),
            AlbumLikesCount = await _context.AlbumLikes.AsNoTracking().CountAsync(cancellationToken),
            BlogLikesCount = await _context.BlogLikes.AsNoTracking().CountAsync(cancellationToken),
            ReelLikesCount = await _context.ReelLikes.AsNoTracking().CountAsync(cancellationToken),
            StoryLikesCount = await _context.StoryLikes.AsNoTracking().CountAsync(cancellationToken),

            AiReviewJobsCount = await _context.AiReviewJobs.AsNoTracking().CountAsync(cancellationToken),
            ContentModerationEventsCount = await _context.ContentModerationEvents.AsNoTracking().CountAsync(cancellationToken),

            OAuthClientsCount = await _context.OAuthClients.AsNoTracking().CountAsync(cancellationToken),
        };

        return Ok(dto);
    }

    private async Task<Dictionary<string, int>> BuildFaceWallTicketStatusCountsAsync(CancellationToken cancellationToken)
    {
        var groups = await _context.FaceWallTickets.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var g in groups)
            dict[g.Key.ToString()] = g.Count;
        foreach (var name in Enum.GetNames<FaceWallTicketStatus>())
        {
            if (!dict.ContainsKey(name))
                dict[name] = 0;
        }

        return dict;
    }

    /// <summary>
    /// Histogram data for dashboard charts. Loads timestamps in range then buckets in-memory — acceptable for demo DB sizes;
    /// switch to database-side grouping if production row counts make this path slow.
    /// </summary>
    /// <param name="metric">users | messages | stories | blogs | reels | albums | friendRequests | wallTickets</param>
    /// <param name="fromUtc">Range start (inclusive).</param>
    /// <param name="toUtc">Range end (inclusive).</param>
    /// <param name="bucket">day (default) or week (ISO week, Monday UTC start).</param>
    [HttpGet("timeseries")]
    public async Task<ActionResult<StatsTimeseriesResponseDto>> GetTimeseries(
        [FromQuery] string metric,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] string bucket = "day",
        CancellationToken cancellationToken = default)
    {
        if (!_access.CanManageAllFaces(User))
        {
            _logger.LogDebug("Stats timeseries denied for user lacking CanManageAllFaces.");
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(metric))
            return BadRequest(new { error = "metric is required." });

        var m = metric.Trim().ToLowerInvariant();
        var allowed = new[] { "users", "messages", "stories", "blogs", "reels", "albums", "friendrequests", "walltickets" };
        if (!allowed.Contains(m))
            return BadRequest(new { error = $"Unknown metric '{metric}'." });

        if (fromUtc > toUtc)
            return BadRequest(new { error = "fromUtc must be less than or equal to toUtc." });

        if ((toUtc - fromUtc).TotalDays > MaxTimeseriesRangeDays)
            return BadRequest(new { error = $"Range must not exceed {MaxTimeseriesRangeDays} days." });

        var b = bucket.Trim().ToLowerInvariant();
        if (b is not ("day" or "week"))
            return BadRequest(new { error = "bucket must be 'day' or 'week'." });

        List<DateTime> timestamps = m switch
        {
            "users" => await _context.Users.AsNoTracking()
                .Where(u => u.CreatedAt >= fromUtc && u.CreatedAt <= toUtc)
                .Select(u => u.CreatedAt)
                .ToListAsync(cancellationToken),
            "messages" => await _context.Messages.AsNoTracking()
                .Where(x => x.SentAt >= fromUtc && x.SentAt <= toUtc)
                .Select(x => x.SentAt)
                .ToListAsync(cancellationToken),
            "stories" => await _context.Stories.AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            "blogs" => await _context.Blogs.AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            "reels" => await _context.Reels.AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            "albums" => await _context.Albums.AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            "friendrequests" => await _context.FriendRequests.AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            "walltickets" => await _context.FaceWallTickets.AsNoTracking()
                .Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken),
            _ => throw new InvalidOperationException("Unreachable metric after validation."),
        };

        var buckets = BucketizeUtc(timestamps, fromUtc, toUtc, b);
        return Ok(new StatsTimeseriesResponseDto
        {
            Metric = m,
            Bucket = b,
            Buckets = buckets,
        });
    }

    /// <summary>
    /// Aggregates UTC instants into contiguous buckets and fills zero-count gaps so chart libraries receive a dense series.
    /// </summary>
    private static IReadOnlyList<StatsTimeseriesBucketDto> BucketizeUtc(
        IReadOnlyList<DateTime> timestamps,
        DateTime fromUtc,
        DateTime toUtc,
        string bucket)
    {
        var counts = new Dictionary<DateTime, int>();

        foreach (var ts in timestamps)
        {
            var utc = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
            var key = bucket == "week" ? StartOfIsoWeekUtc(utc) : utc.Date;
            counts.TryGetValue(key, out var c);
            counts[key] = c + 1;
        }

        var step = bucket == "week" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
        var start = bucket == "week" ? StartOfIsoWeekUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)) : fromUtc.Date;
        var end = bucket == "week" ? StartOfIsoWeekUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)) : toUtc.Date;

        var result = new List<StatsTimeseriesBucketDto>();
        for (var cursor = start; cursor <= end; cursor += step)
        {
            counts.TryGetValue(cursor, out var n);
            result.Add(new StatsTimeseriesBucketDto { PeriodStartUtc = cursor, Count = n });
        }

        return result;
    }

    /// <summary>Returns Monday 00:00 UTC for the ISO week containing <paramref name="utcInstant"/>.</summary>
    private static DateTime StartOfIsoWeekUtc(DateTime utcInstant)
    {
        var utc = utcInstant.Kind == DateTimeKind.Utc ? utcInstant : utcInstant.ToUniversalTime();
        var year = ISOWeek.GetYear(utc);
        var week = ISOWeek.GetWeekOfYear(utc);
        return ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
    }
}
