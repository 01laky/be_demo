using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// J6: access JWT carries <c>atv</c> claim; bumping <see cref="Models.ApplicationUser.AccessTokenVersion"/> invalidates outstanding access tokens.
/// </summary>
public class AccessTokenVersionTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public AccessTokenVersionTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Authenticated_Request_Fails_After_AccessTokenVersion_Bump()
    {
        var client = _factory.CreateClient();
        var email = $"atv_{Guid.NewGuid():N}@test.com";
        var reg = await client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenReq = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = "Test123!@#",
        };
        var tokenRes = await client.PostAsJsonAsync("/api/oauth2/token", tokenReq);
        tokenRes.EnsureSuccessStatusCode();
        var tokenDto = await tokenRes.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        tokenDto.Should().NotBeNull();
        var access = tokenDto!.AccessToken;
        access.Should().NotBeNullOrEmpty();

        var faceClient = _factory.CreateFaceClient("public");
        faceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var before = await faceClient.GetAsync("/api/me/capabilities");
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == email);
            var tracked = await db.Users.FirstAsync(u => u.Id == user.Id);
            tracked.AccessTokenVersion++;
            await db.SaveChangesAsync();
        }

        var after = await faceClient.GetAsync("/api/me/capabilities");
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticated_Request_Fails_After_Global_UserRoleId_Change()
    {
        var client = _factory.CreateClient();
        var email = $"atv_role_{Guid.NewGuid():N}@test.com";
        var reg = await client.PostAsJsonAsync("/api/oauth2/register", new { email, password = "Test123!@#" });
        reg.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenReq = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = "Test123!@#",
        };
        var tokenRes = await client.PostAsJsonAsync("/api/oauth2/token", tokenReq);
        tokenRes.EnsureSuccessStatusCode();
        var access = (await tokenRes.Content.ReadFromJsonAsync<OAuth2TokenResponse>())!.AccessToken!;

        var faceClient = _factory.CreateFaceClient("public");
        faceClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
        (await faceClient.GetAsync("/api/me/capabilities")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userRole = await db.UserRoles.AsNoTracking().FirstAsync(r => r.Name == UserRole.GlobalRoleNames.Admin);
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.UserRoleId = userRole.Id;
            await db.SaveChangesAsync();
        }

        var after = await faceClient.GetAsync("/api/me/capabilities");
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
