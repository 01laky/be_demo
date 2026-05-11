using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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

    public void Dispose() => _client.Dispose();
}
