using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// HTTP clients for ACL tests: OAuth is unscoped; API calls use face-prefixed clients.
/// </summary>
public static class AclTestClients
{
    public static HttpClient CreateOAuthClient(CustomWebApplicationFactory<Program> factory) =>
        factory.CreateUnscopedClient();

    /// <summary>Tenant-scoped client (default public face).</summary>
    public static HttpClient CreatePublicFaceClient(CustomWebApplicationFactory<Program> factory) =>
        factory.CreateFaceClient("public");

    /// <summary>Platform admin UI scope + same routing as production.</summary>
    public static HttpClient CreateAdminFaceClient(CustomWebApplicationFactory<Program> factory) =>
        factory.CreateFaceClient("admin");

    public static async Task<string> RegisterAndGetTokenAsync(HttpClient oauthClient, string? email = null, string password = "Test123!@#")
    {
        email ??= $"acl_{Guid.NewGuid():N}@test.com";
        var reg = await oauthClient.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Acl",
            lastName = "User",
        });
        reg.EnsureSuccessStatusCode();

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password,
        };

        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            var response = await oauthClient.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
                if (!string.IsNullOrEmpty(body?.AccessToken))
                    return body.AccessToken;
            }
        }

        throw new InvalidOperationException("Failed to obtain token for " + email);
    }

    public static async Task<string> GetPlatformAdminTokenAsync(HttpClient oauthClient) =>
        await IntegrationTestSeed.GetAdminAccessTokenAsync(oauthClient);

    public static async Task<string> GetPlatformSuperAdminTokenAsync(HttpClient oauthClient) =>
        await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(oauthClient);
}
