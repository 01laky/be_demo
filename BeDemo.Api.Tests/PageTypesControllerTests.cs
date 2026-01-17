/*
 * PageTypesControllerTests.cs - Unit tests for PageTypesController
 * 
 * Tests all endpoints in PageTypesController:
 * - GET /api/pagetypes - Get all page types
 * - GET /api/pagetypes/{id} - Get page type by ID
 * - POST /api/pagetypes - Create new page type
 * - PUT /api/pagetypes/{id} - Update page type
 * - DELETE /api/pagetypes/{id} - Delete page type (with validation)
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for PageTypesController
/// </summary>
public class PageTypesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string? _authToken;

    public PageTypesControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Helper method to authenticate and get JWT token
    /// </summary>
    private async Task<string> GetAuthTokenAsync()
    {
        if (_authToken != null)
            return _authToken;

        var email = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Test123!@#";

        await _client.PostAsJsonAsync("/api/oauth2/register", new
        {
            email,
            password,
            firstName = "Admin",
            lastName = "User"
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

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        tokenResponse.Should().NotBeNull();
        _authToken = tokenResponse!.AccessToken;
        return _authToken;
    }

    [Fact]
    public async Task GetPageTypes_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Act
        var response = await _client.GetAsync("/api/pagetypes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPageTypes_ShouldReturnPageTypesList_WhenAuthenticated()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/pagetypes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pageTypes = await response.Content.ReadFromJsonAsync<List<object>>();
        pageTypes.Should().NotBeNull();
        pageTypes!.Should().BeAssignableTo<IEnumerable<object>>();
    }

    [Fact]
    public async Task GetPageType_ShouldReturnPageType_WhenValidId()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/pagetypes", new
        {
            index = $"test_{Guid.NewGuid()}"
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPageType = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        createdPageType.Should().NotBeNull();
        int pageTypeId = (int)createdPageType.GetProperty("id").GetInt32();

        // Act
        var response = await _client.GetAsync($"/api/pagetypes/{pageTypeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pageType = await response.Content.ReadFromJsonAsync<JsonElement>();
        pageType.Should().NotBeNull();
        pageType.GetProperty("id").GetInt32().Should().Be(pageTypeId);
    }

    [Fact]
    public async Task CreatePageType_ShouldReturnCreated_WhenValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createRequest = new
        {
            index = $"test_{Guid.NewGuid()}"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pagetypes", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var pageType = await response.Content.ReadFromJsonAsync<JsonElement>();
        pageType.Should().NotBeNull();
        pageType.GetProperty("index").GetString().Should().Be(createRequest.index);
    }

    [Fact]
    public async Task UpdatePageType_ShouldReturnOk_WhenValidData()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/pagetypes", new
        {
            index = $"test_{Guid.NewGuid()}"
        });

        var createdPageType = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        int pageTypeId = (int)createdPageType.GetProperty("id").GetInt32();

        var updateRequest = new
        {
            index = $"updated_{Guid.NewGuid()}"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/pagetypes/{pageTypeId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedPageType = await response.Content.ReadFromJsonAsync<JsonElement>();
        updatedPageType.Should().NotBeNull();
        updatedPageType.GetProperty("index").GetString().Should().Be(updateRequest.index);
    }

    [Fact]
    public async Task DeletePageType_ShouldReturnNoContent_WhenValidId()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/pagetypes", new
        {
            index = $"test_{Guid.NewGuid()}"
        });

        var createdPageType = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        int pageTypeId = (int)createdPageType.GetProperty("id").GetInt32();

        // Act
        var response = await _client.DeleteAsync($"/api/pagetypes/{pageTypeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
