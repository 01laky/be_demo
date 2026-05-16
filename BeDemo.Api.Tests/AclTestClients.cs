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

    public static Task<string> RegisterAndGetTokenAsync(
        CustomWebApplicationFactory<Program> factory,
        HttpClient oauthClient,
        string? email = null,
        string password = "Test123!@#") =>
        IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
            oauthClient,
            factory,
            email,
            password,
            "Acl",
            "User");

    public static async Task<string> GetPlatformAdminTokenAsync(HttpClient oauthClient) =>
        await IntegrationTestSeed.GetAdminAccessTokenAsync(oauthClient);

    public static async Task<string> GetPlatformSuperAdminTokenAsync(HttpClient oauthClient) =>
        await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(oauthClient);
}
