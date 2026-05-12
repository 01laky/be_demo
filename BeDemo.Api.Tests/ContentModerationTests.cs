using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class ContentModerationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ContentModerationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData(ContentApprovalStatus.PendingApproval, AiReviewStatus.Queued, "Pending approval")]
    [InlineData(ContentApprovalStatus.PendingApproval, AiReviewStatus.InProgress, "Under AI review")]
    [InlineData(ContentApprovalStatus.PendingApproval, AiReviewStatus.NeedsHumanReview, "Needs review")]
    [InlineData(ContentApprovalStatus.Approved, AiReviewStatus.RecommendedApprove, "Approved")]
    [InlineData(ContentApprovalStatus.Rejected, AiReviewStatus.RecommendedReject, "Rejected")]
    [InlineData(ContentApprovalStatus.Removed, AiReviewStatus.Failed, "Removed")]
    public void CreatorStatusLabel_ShouldMapSafePublicCopy(
        ContentApprovalStatus approvalStatus,
        AiReviewStatus aiReviewStatus,
        string expected)
    {
        ContentModerationHelpers.CreatorStatusLabel(approvalStatus, aiReviewStatus).Should().Be(expected);
    }

    [Fact]
    public void ValidateRecommendation_ShouldSendInvalidPayloadsToHumanReview()
    {
        var invalidConfidence = new AiReviewRecommendation(
            AiReviewDecision.Approve,
            1.5,
            AiReviewRiskLevel.Low,
            Array.Empty<string>(),
            "Looks fine",
            "Safe",
            "moderation-v1",
            "trace");

        ContentModerationHelpers.ValidateRecommendation(invalidConfidence).IsValid.Should().BeFalse();

        var highRiskApprove = invalidConfidence with { Confidence = 0.9, RiskLevel = AiReviewRiskLevel.High };
        ContentModerationHelpers.ValidateRecommendation(highRiskApprove).IsValid.Should().BeFalse();

        var rejectWithoutReason = invalidConfidence with
        {
            Decision = AiReviewDecision.Reject,
            Confidence = 0.8,
            RiskLevel = AiReviewRiskLevel.Medium,
            Reason = null
        };
        ContentModerationHelpers.ValidateRecommendation(rejectWithoutReason).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://cdn.example.com/file.mp4", true)]
    [InlineData("http://cdn.example.com/file.mp4", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/relative/file.mp4", false)]
    public void IsSafeHttpUrl_ShouldAllowOnlyAbsoluteHttpUrls(string value, bool expected)
    {
        ContentModerationHelpers.IsSafeHttpUrl(value).Should().Be(expected);
    }

    [Fact]
    public async Task ContentAiReviewService_ShouldStoreRecommendationWithoutPublishingContent()
    {
        await using var context = CreateContext();
        var face = new Face { Index = $"face-{Guid.NewGuid():N}", Title = "Review Face" };
        var user = CreateUser("ai-review-user");
        context.Faces.Add(face);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var blog = new Blog
        {
            CreatorId = user.Id,
            FaceId = face.Id,
            Title = "Safe community update",
            Content = "<p>Useful community content.</p>",
            ApprovalStatus = ContentApprovalStatus.PendingApproval,
            AiReviewStatus = AiReviewStatus.Queued,
            SubmittedAtUtc = DateTime.UtcNow,
            ModerationVersion = 1,
        };
        context.Blogs.Add(blog);
        await context.SaveChangesAsync();
        context.AiReviewJobs.Add(new AiReviewJob
        {
            ContentType = ModeratedContentType.Blog,
            ContentId = blog.Id,
            FaceId = face.Id,
            CreatedByUserId = user.Id,
            ModerationVersion = 1,
        });
        await context.SaveChangesAsync();

        var ai = new FakeAiGrpcService(new AiReviewRecommendation(
            AiReviewDecision.Approve,
            0.94,
            AiReviewRiskLevel.Low,
            Array.Empty<string>(),
            "Looks safe.",
            "Your content is waiting for final approval.",
            "test-model",
            "trace-1"));
        var service = CreateReviewService(context, ai);

        await service.ProcessQueuedReviewAsync(ContentModerationHelpers.BuildAiReviewPayload(
            ModeratedContentType.Blog,
            blog.Id,
            1));

        blog.ApprovalStatus.Should().Be(ContentApprovalStatus.PendingApproval);
        blog.AiReviewStatus.Should().Be(AiReviewStatus.RecommendedApprove);
        blog.AiReviewDecision.Should().Be(AiReviewDecision.Approve);
        blog.AiReviewConfidence.Should().Be(0.94);
        blog.AiReviewModelVersion.Should().Be("test-model");
        context.AiReviewJobs.Single().Status.Should().Be(AiReviewJobStatus.Completed);
        context.ContentModerationEvents.Select(e => e.NewAiReviewStatus).Should().Contain(AiReviewStatus.RecommendedApprove);
    }

    [Fact]
    public async Task ContentAiReviewService_ShouldRetryFailure_ThenFallbackToHumanReview()
    {
        await using var context = CreateContext();
        var face = new Face { Index = $"face-{Guid.NewGuid():N}", Title = "Retry Face" };
        var user = CreateUser("ai-retry-user");
        context.Faces.Add(face);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var blog = new Blog
        {
            CreatorId = user.Id,
            FaceId = face.Id,
            Title = "Retry me",
            Content = "<p>AI outage</p>",
            ApprovalStatus = ContentApprovalStatus.PendingApproval,
            AiReviewStatus = AiReviewStatus.Queued,
            ModerationVersion = 1,
        };
        context.Blogs.Add(blog);
        var job = new AiReviewJob
        {
            ContentType = ModeratedContentType.Blog,
            ContentId = blog.Id,
            FaceId = face.Id,
            CreatedByUserId = user.Id,
            ModerationVersion = 1,
            MaxAttempts = 2,
        };
        await context.SaveChangesAsync();
        context.AiReviewJobs.Add(job);
        await context.SaveChangesAsync();

        var queue = new CapturingRedisJobQueue();
        var service = CreateReviewService(context, new FakeAiGrpcService("timeout"), queue);
        var payload = ContentModerationHelpers.BuildAiReviewPayload(ModeratedContentType.Blog, blog.Id, 1);

        await service.ProcessQueuedReviewAsync(payload);
        job.Status.Should().Be(AiReviewJobStatus.RetryScheduled);
        blog.AiReviewStatus.Should().Be(AiReviewStatus.Queued);
        queue.Scheduled.Should().ContainSingle();

        await service.ProcessQueuedReviewAsync(payload);
        job.Status.Should().Be(AiReviewJobStatus.NeedsHumanReview);
        blog.AiReviewStatus.Should().Be(AiReviewStatus.NeedsHumanReview);
        blog.AiReviewDecision.Should().Be(AiReviewDecision.NeedsHumanReview);
    }

    [Fact]
    public async Task ContentAiReviewService_ShouldIgnoreStaleModerationVersion()
    {
        await using var context = CreateContext();
        var face = new Face { Index = $"face-{Guid.NewGuid():N}", Title = "Stale Face" };
        var user = CreateUser("ai-stale-user");
        context.Faces.Add(face);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var blog = new Blog
        {
            CreatorId = user.Id,
            FaceId = face.Id,
            Title = "Stale",
            Content = "<p>Edited later</p>",
            ApprovalStatus = ContentApprovalStatus.PendingApproval,
            AiReviewStatus = AiReviewStatus.Queued,
            ModerationVersion = 2,
        };
        context.Blogs.Add(blog);
        await context.SaveChangesAsync();
        var staleJob = new AiReviewJob
        {
            ContentType = ModeratedContentType.Blog,
            ContentId = blog.Id,
            FaceId = face.Id,
            CreatedByUserId = user.Id,
            ModerationVersion = 1,
        };
        context.AiReviewJobs.Add(staleJob);
        await context.SaveChangesAsync();

        var service = CreateReviewService(context, new FakeAiGrpcService("should not be called"));
        await service.ProcessQueuedReviewAsync(ContentModerationHelpers.BuildAiReviewPayload(
            ModeratedContentType.Blog,
            blog.Id,
            1));

        staleJob.Status.Should().Be(AiReviewJobStatus.Failed);
        blog.AiReviewStatus.Should().Be(AiReviewStatus.Queued);
    }

    [Fact]
    public async Task ContentModerationMetrics_ShouldReturnEmptySnapshotSafely()
    {
        await using var context = CreateContext();
        var metrics = new ContentModerationMetrics(context);

        var snapshot = await metrics.GetSnapshotAsync();

        snapshot.PendingSubmissions.Should().Be(0);
        snapshot.OldestPendingSubmissionUtc.Should().BeNull();
    }

    [Fact]
    public async Task ModerationActions_ShouldAllowOnlySuperAdmin()
    {
        var userToken = await RegisterAndLoginAsync("moderation_user");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var create = await _client.PostAsJsonAsync("/api/blogs", new
        {
            title = "Moderate Me",
            content = "<p>Needs review</p>",
            faceId = await GetPublicFaceIdAsync(_client),
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var blogId = created.GetProperty("id").GetInt32();

        var userApprove = await _client.PostAsJsonAsync(
            $"/api/contentmoderation/{ModeratedContentType.Blog}/{blogId}/approve",
            new { reason = "self approve" });
        userApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var admin = _factory.CreateFaceClient("admin");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await IntegrationTestSeed.GetAdminAccessTokenAsync(admin));
        var adminApprove = await admin.PostAsJsonAsync(
            $"/api/contentmoderation/{ModeratedContentType.Blog}/{blogId}/approve",
            new { reason = "admin approve" });
        adminApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var superAdmin = _factory.CreateFaceClient("admin");
        superAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(superAdmin));
        var superApprove = await superAdmin.PostAsJsonAsync(
            $"/api/contentmoderation/{ModeratedContentType.Blog}/{blogId}/approve",
            new { reason = "superadmin approve" });
        superApprove.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await superAdmin.GetFromJsonAsync<JsonElement[]>(
            $"/api/contentmoderation/{ModeratedContentType.Blog}/{blogId}/events");
        events!.Should().NotBeEmpty();
        events.Select(e => e.GetProperty("newApprovalStatus").GetString()).Should().Contain("Approved");
    }

    [Fact]
    public async Task RejectAndRemove_ShouldRequireReason_AndWriteAudit()
    {
        var userToken = await RegisterAndLoginAsync("moderation_reject");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var create = await _client.PostAsJsonAsync("/api/albums", new
        {
            title = "Reject Me",
            albumType = 1,
            mediaType = 1
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var albumId = created.GetProperty("id").GetInt32();

        using var superAdmin = _factory.CreateFaceClient("admin");
        superAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(superAdmin));

        var missingReason = await superAdmin.PostAsJsonAsync(
            $"/api/contentmoderation/{ModeratedContentType.Album}/{albumId}/reject",
            new { reason = "" });
        missingReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reject = await superAdmin.PostAsJsonAsync(
            $"/api/contentmoderation/{ModeratedContentType.Album}/{albumId}/reject",
            new { reason = "Policy mismatch", userMessage = "Please update the album." });
        reject.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await reject.Content.ReadFromJsonAsync<JsonElement>();
        rejected.GetProperty("approvalStatus").GetString().Should().Be("Rejected");

        var remove = await superAdmin.PostAsJsonAsync(
            $"/api/contentmoderation/{ModeratedContentType.Album}/{albumId}/remove",
            new { reason = "Escalated policy incident" });
        remove.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await superAdmin.GetFromJsonAsync<JsonElement[]>(
            $"/api/contentmoderation/{ModeratedContentType.Album}/{albumId}/events");
        events!.Select(e => e.GetProperty("newApprovalStatus").GetString()).Should().Contain("Removed");
    }

    private async Task<string> RegisterAndLoginAsync(string prefix)
    {
        var email = $"{prefix}_{Guid.NewGuid()}@test.com";
        const string password = "Test123!@#";
        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Moderation",
            lastName = "Tester"
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password
        };

        HttpResponseMessage? response = null;
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        return tokenResponse!.AccessToken;
    }

    private static async Task<int> GetPublicFaceIdAsync(HttpClient client)
    {
        var cfg = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
        cfg.Should().NotBeNull();
        return cfg![0].GetProperty("id").GetInt32();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"content-moderation-{Guid.NewGuid():N}")
            .Options;
        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static ApplicationUser CreateUser(string prefix) => new()
    {
        Id = $"{prefix}-{Guid.NewGuid():N}",
        UserName = $"{prefix}@example.com",
        Email = $"{prefix}@example.com",
        UserRoleId = 1,
    };

    private static ContentAiReviewService CreateReviewService(
        ApplicationDbContext context,
        IAiGrpcService ai,
        IRedisJobQueue? queue = null) =>
        new(
            context,
            ai,
            queue ?? new CapturingRedisJobQueue(),
            NullLogger<ContentAiReviewService>.Instance);

    private sealed class FakeAiGrpcService : IAiGrpcService
    {
        private readonly AiReviewRecommendation? _recommendation;
        private readonly string? _error;

        public FakeAiGrpcService(AiReviewRecommendation recommendation)
        {
            _recommendation = recommendation;
        }

        public FakeAiGrpcService(string error)
        {
            _error = error;
        }

        public Task<string> GenerateAsync(string prompt, int maxNewTokens = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task<AiContentReviewResult> ReviewContentAsync(
            AiContentReviewRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_recommendation == null
                ? new AiContentReviewResult(null, _error)
                : new AiContentReviewResult(_recommendation, null));
    }

    private sealed class CapturingRedisJobQueue : IRedisJobQueue
    {
        public List<(string JobType, string PayloadJson, DateTime RunAtUtc)> Scheduled { get; } = new();

        public Task EnqueueAsync(string jobType, string payloadJson, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ScheduleAsync(
            string jobType,
            string payloadJson,
            DateTime runAtUtc,
            CancellationToken cancellationToken = default)
        {
            Scheduled.Add((jobType, payloadJson, runAtUtc));
            return Task.CompletedTask;
        }
    }

    public void Dispose() => _client.Dispose();
}
