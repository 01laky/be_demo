using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BeDemo.Api.Services;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class OAuth2ServiceTests
{
    private readonly Mock<IECDSAKeyService> _mockKeyService;
    private readonly Mock<ILogger<OAuth2Service>> _mockLogger;
    private readonly IConfiguration _configuration;

    public OAuth2ServiceTests()
    {
        _mockKeyService = new Mock<IECDSAKeyService>();
        _mockLogger = new Mock<ILogger<OAuth2Service>>();

        // Setup configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "OAuth2:ClientId", "test-client" },
            { "OAuth2:ClientSecret", "test-secret" },
            { "Jwt:Issuer", "TestApi" },
            { "Jwt:Audience", "TestApi" },
            { "Jwt:ExpiresInMinutes", "60" }
        });
        _configuration = configBuilder.Build();

        // Setup mock key service
        _mockKeyService.Setup(x => x.GetKeyId()).Returns("test-key-id");
        var ecdsa = System.Security.Cryptography.ECDsa.Create();
        var securityKey = new Microsoft.IdentityModel.Tokens.ECDsaSecurityKey(ecdsa);
        _mockKeyService.Setup(x => x.GetSigningKey()).Returns(securityKey);
        _mockKeyService.Setup(x => x.GetValidationKey()).Returns(securityKey);
    }

    [Fact]
    public async Task ValidateClientAsync_ShouldReturnTrue_WhenCredentialsAreValid()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);

        // Act
        var result = await service.ValidateClientAsync("test-client", "test-secret");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientIdIsInvalid()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);

        // Act
        var result = await service.ValidateClientAsync("invalid-client", "test-secret");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientSecretIsInvalid()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);

        // Act
        var result = await service.ValidateClientAsync("test-client", "invalid-secret");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientIdIsNull()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);

        // Act
        var result = await service.ValidateClientAsync(null, "test-secret");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateClientAsync_ShouldReturnFalse_WhenClientSecretIsNull()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);

        // Act
        var result = await service.ValidateClientAsync("test-client", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRequestSignature_ShouldReturnFalse_WhenSignatureIsMissing()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);
        var request = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "test-client",
            ClientSecret = "test-secret"
        };

        // Act
        var result = service.ValidateRequestSignature(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateRequestSignature_ShouldReturnFalse_WhenAlgorithmIsMissing()
    {
        // Arrange
        var service = new OAuth2Service(_mockKeyService.Object, _configuration, _mockLogger.Object);
        var request = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Signature = "test-signature"
        };

        // Act
        var result = service.ValidateRequestSignature(request);

        // Assert
        result.Should().BeFalse();
    }
}
