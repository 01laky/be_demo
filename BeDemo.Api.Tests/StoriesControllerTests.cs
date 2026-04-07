using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class StoriesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public StoriesControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<(string Token, string UserId)> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"st_{Guid.NewGuid()}@test.com";
        const string password = "Test123!@#";
        await client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "St",
            lastName = "User",
        });

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password,
        };

        HttpResponseMessage? response = null;
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        var token = tokenResponse!.AccessToken;
        var payload = token.Split('.')[1];
        var pad = payload.Length % 4 == 0 ? "" : new string('=', 4 - payload.Length % 4);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload + pad));
        var doc = JsonDocument.Parse(json);
        var userId = doc.RootElement.TryGetProperty("nameid", out var n) ? n.GetString() : doc.RootElement.GetProperty("sub").GetString();
        userId.Should().NotBeNullOrEmpty();
        return (token, userId!);
    }

    private static async Task<int> GetAnyFaceIdAsync(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cfg = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
        cfg.Should().NotBeNullOrEmpty();
        return cfg![0].GetProperty("id").GetInt32();
    }

    private static async Task<int> GetFaceRoleIdAsync(HttpClient client, string token, string roleNameSubstring)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        roles.Should().NotBeNull();
        foreach (var r in roles!)
        {
            var name = r.GetProperty("name").GetString() ?? "";
            if (name.Contains(roleNameSubstring, StringComparison.OrdinalIgnoreCase))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {roleNameSubstring} not found");
    }

    private static async Task<int> CreatePublishedStoryWithOneImageAsync(HttpClient client, string token, int faceId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync("/api/stories", new { title = "T1", faceIds = Array.Empty<int>() });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var storyId = created!.GetProperty("id").GetInt32();

        using var mp = new MultipartFormDataContent();
        var img = new ByteArrayContent(new byte[] { 0xff, 0xd8, 0xff, 0xe0 });
        img.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        mp.Add(img, "file", "x.jpg");
        mp.Add(new StringContent("d1"), "description");
        mp.Add(new StringContent("0"), "sortOrder");
        var up = await client.PostAsync($"/api/stories/{storyId}/images", mp);
        up.StatusCode.Should().Be(HttpStatusCode.OK);

        var pub = await client.PostAsJsonAsync($"/api/stories/{storyId}/publish", new { scheduledPublishAt = (DateTime?)null });
        pub.StatusCode.Should().Be(HttpStatusCode.OK);
        return storyId;
    }

    [Fact]
    public async Task ListForFace_ShouldBeEmpty_ForHostOnlyUser()
    {
        using var client = _factory.CreateClient();
        var (token, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/stories?faceId={faceId}");
        list.Should().NotBeNull();
        list!.Length.Should().Be(0);
    }

    [Fact]
    public async Task PublishAndList_ShouldWork_ForNonHostUser()
    {
        using var client = _factory.CreateClient();
        var (token, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var storyId = await CreatePublishedStoryWithOneImageAsync(client, token, faceId);

        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/stories?faceId={faceId}");
        list.Should().NotBeNull();
        list!.Select(e => e.GetProperty("id").GetInt32()).Should().Contain(storyId);
    }

    [Fact]
    public async Task ViewLikeComment_AndCreatorSeesViewers()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();
        var (tokenA, _) = await RegisterAndLoginAsync(clientA);
        var (tokenB, userB) = await RegisterAndLoginAsync(clientB);
        var faceId = await GetAnyFaceIdAsync(clientA, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientA, tokenA, "FACE_USER");

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientA.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();
        var storyId = await CreatePublishedStoryWithOneImageAsync(clientA, tokenA, faceId);

        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        (await clientB.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var view = await clientB.PostAsync($"/api/stories/{storyId}/view?faceId={faceId}", null);
        view.StatusCode.Should().Be(HttpStatusCode.OK);

        var like = await clientB.PostAsync($"/api/stories/{storyId}/likes?faceId={faceId}", null);
        like.StatusCode.Should().Be(HttpStatusCode.OK);

        var comment = await clientB.PostAsJsonAsync($"/api/stories/{storyId}/comments?faceId={faceId}", new { content = "nice" });
        comment.StatusCode.Should().Be(HttpStatusCode.OK);

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var detail = await clientA.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        detail.GetProperty("viewCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        var viewers = detail.GetProperty("viewers").EnumerateArray().ToList();
        viewers.Should().Contain(v => v.GetProperty("viewerUserId").GetString() == userB);
    }

    [Fact]
    public async Task Expire_ShouldClearInteractions_AndAllowPublishAgain()
    {
        using var client = _factory.CreateClient();
        var (token, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var storyId = await CreatePublishedStoryWithOneImageAsync(client, token, faceId);

        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = await ctx.Stories.FindAsync(storyId);
            s!.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await ctx.SaveChangesAsync();
            var life = scope.ServiceProvider.GetRequiredService<IStoryLifecycleService>();
            await life.ApplyExpireAsync(storyId);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        detail.GetProperty("state").GetString().Should().Be("Expired");
        detail.GetProperty("likesCount").GetInt32().Should().Be(0);

        var pub = await client.PostAsJsonAsync($"/api/stories/{storyId}/publish", new { scheduledPublishAt = (DateTime?)null });
        pub.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await client.GetFromJsonAsync<JsonElement>($"/api/stories/{storyId}?faceId={faceId}");
        after.GetProperty("state").GetString().Should().Be("Published");
    }

    [Fact]
    public async Task DeleteDraft_ShouldReturn204()
    {
        using var client = _factory.CreateClient();
        var (token, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var create = await client.PostAsJsonAsync("/api/stories", new { title = "Del", faceIds = new[] { faceId } });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        var del = await client.DeleteAsync($"/api/stories/{id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
