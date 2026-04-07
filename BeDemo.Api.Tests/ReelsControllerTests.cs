using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class ReelsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _authToken;

    public ReelsControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null)
            return _authToken;

        var email = $"reel_test_{Guid.NewGuid()}@test.com";
        const string password = "Test123!@#";

        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Reel",
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
        _authToken = tokenResponse!.AccessToken;
        return _authToken;
    }

    private void SetAuth(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<int> CreateTestFaceAsync()
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync("/api/faces", new
        {
            index = $"reel_test_{Guid.NewGuid()}",
            title = "Reel Test Face",
            description = "For reel tests"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var face = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return face.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateTestReelAsync(List<int>? faceIds = null, string? videoUrl = null)
    {
        var token = await GetAuthTokenAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync("/api/reels", new
        {
            title = $"Reel {Guid.NewGuid()}",
            description = "Test",
            videoUrl = videoUrl ?? "https://example.com/video.mp4",
            faceIds
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var reel = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return reel.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetReels_ShouldReturnUnauthorized_WhenNoToken()
    {
        var response = await _client.GetAsync("/api/reels");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReels_ShouldReturnList_WhenAuthenticated()
    {
        SetAuth(await GetAuthTokenAsync());
        var response = await _client.GetAsync("/api/reels");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reels = await response.Content.ReadFromJsonAsync<JsonElement>();
        reels.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task CreateReel_ShouldReturnCreated_WithValidData()
    {
        SetAuth(await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/reels", new
        {
            title = "My Reel",
            description = "Desc",
            videoUrl = "https://cdn.example.com/v.mp4"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reel = await response.Content.ReadFromJsonAsync<JsonElement>();
        reel.GetProperty("title").GetString().Should().Be("My Reel");
        reel.GetProperty("videoUrl").GetString().Should().Be("https://cdn.example.com/v.mp4");
    }

    [Fact]
    public async Task CreateReel_ShouldReturnBadRequest_WhenVideoUrlMissing()
    {
        SetAuth(await GetAuthTokenAsync());

        var response = await _client.PostAsJsonAsync("/api/reels", new
        {
            title = "No video"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetReels_WithFaceId_ShouldFilterScopedReels()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceA = await CreateTestFaceAsync();
        var faceB = await CreateTestFaceAsync();

        var scopedId = await CreateTestReelAsync(new List<int> { faceA });
        await CreateTestReelAsync(null);

        var onA = await _client.GetAsync($"/api/reels?faceId={faceA}");
        onA.StatusCode.Should().Be(HttpStatusCode.OK);
        var arrA = await onA.Content.ReadFromJsonAsync<JsonElement>();
        var idsA = arrA.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToHashSet();
        idsA.Should().Contain(scopedId);

        var onB = await _client.GetAsync($"/api/reels?faceId={faceB}");
        onB.StatusCode.Should().Be(HttpStatusCode.OK);
        var arrB = await onB.Content.ReadFromJsonAsync<JsonElement>();
        var idsB = arrB.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToHashSet();
        idsB.Should().NotContain(scopedId);
    }

    [Fact]
    public async Task GetReel_ScopedWithoutFaceId_ShouldReturnNotFound()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();
        var reelId = await CreateTestReelAsync(new List<int> { faceId });

        var response = await _client.GetAsync($"/api/reels/{reelId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetReel_ScopedWithMatchingFaceId_ShouldReturnOk()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();
        var reelId = await CreateTestReelAsync(new List<int> { faceId });

        var response = await _client.GetAsync($"/api/reels/{reelId}?faceId={faceId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReelComments_ShouldRequireFaceId_WhenScoped()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();
        var reelId = await CreateTestReelAsync(new List<int> { faceId });

        var noFace = await _client.GetAsync($"/api/reels/{reelId}/comments");
        noFace.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ok = await _client.GetAsync($"/api/reels/{reelId}/comments?faceId={faceId}");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReelLike_ShouldWork_WhenVisible()
    {
        SetAuth(await GetAuthTokenAsync());
        var faceId = await CreateTestFaceAsync();
        var reelId = await CreateTestReelAsync(new List<int> { faceId });

        var like = await _client.PostAsync($"/api/reels/{reelId}/likes?faceId={faceId}", null);
        like.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteReel_ShouldReturnNoContent_WhenCreator()
    {
        SetAuth(await GetAuthTokenAsync());
        var reelId = await CreateTestReelAsync();

        var response = await _client.DeleteAsync($"/api/reels/{reelId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
