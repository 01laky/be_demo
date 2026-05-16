using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;

namespace BeDemo.Api.Tests.Validation.Integration;

public sealed class ValidationProblemDetailsIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ValidationProblemDetailsIntegrationTests(CustomWebApplicationFactory<Program> factory) =>
        _client = factory.CreateUnscopedClient();

    [Fact]
    public async Task Register_request_with_invalid_email_returns_problem_details()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/oauth2/register/request",
            new RegisterRequestDto { Email = "not-an-email" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("errors");
    }

    [Fact]
    public async Task OAuth2_token_missing_grant_type_returns_oauth_error_not_problem_details()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/oauth2/token",
            new OAuth2TokenRequest
            {
                GrantType = "",
                ClientId = "be-demo-client",
                ClientSecret = "be-demo-secret-very-strong-key",
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("invalid_request");
        body.Should().NotContain("\"errors\"");
    }
}
