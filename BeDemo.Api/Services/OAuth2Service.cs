/*
 * OAuth2Service.cs - Orchestrates OAuth2 token grants (password, refresh_token).
 *
 * Client validation, access JWT creation, and body-signature verification are delegated to focused services.
 */

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Interface for OAuth2 service - defines contract for OAuth2 operations
/// </summary>
public interface IOAuth2Service
{
    /// <summary>
    /// Generates JWT access token and refresh token for user
    /// </summary>
    Task<OAuth2TokenResponse?> GenerateTokenAsync(OAuth2TokenRequest request, UserManager<ApplicationUser> userManager);

    /// <summary>
    /// Validates ECDSA request signature (legacy; middleware rejects signed bodies for token requests).
    /// </summary>
    bool ValidateRequestSignature(OAuth2TokenRequest request);

    /// <summary>
    /// Validates client credentials (client_id and client_secret)
    /// </summary>
    Task<bool> ValidateClientAsync(string? clientId, string? clientSecret);
}

/// <summary>
/// OAuth2 token endpoint orchestration: password and refresh_token grants.
/// </summary>
public sealed class OAuth2Service : IOAuth2Service
{
    private readonly IOAuthAccessTokenFactory _accessTokens;
    private readonly IOAuthClientValidator _clientValidator;
    private readonly IOAuthTokenRequestSignatureVerifier _signatureVerifier;
    private readonly IOAuthRefreshTokenStore _refreshTokens;
    private readonly ILogger<OAuth2Service> _logger;

    public OAuth2Service(
        IOAuthAccessTokenFactory accessTokens,
        IOAuthClientValidator clientValidator,
        IOAuthTokenRequestSignatureVerifier signatureVerifier,
        IOAuthRefreshTokenStore refreshTokens,
        ILogger<OAuth2Service> logger)
    {
        _accessTokens = accessTokens;
        _clientValidator = clientValidator;
        _signatureVerifier = signatureVerifier;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<bool> ValidateClientAsync(string? clientId, string? clientSecret) =>
        _clientValidator.ValidateAsync(clientId, clientSecret);

    /// <inheritdoc />
    public bool ValidateRequestSignature(OAuth2TokenRequest request) =>
        _signatureVerifier.IsSignatureValid(request);

    /// <inheritdoc />
    public async Task<OAuth2TokenResponse?> GenerateTokenAsync(
        OAuth2TokenRequest request,
        UserManager<ApplicationUser> userManager)
    {
        switch (request.GrantType.ToLowerInvariant())
        {
            case "password":
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("Password grant type missing username or password");
                    return null;
                }

                var userByCreds = await userManager.FindByEmailAsync(request.Username)
                    .ConfigureAwait(false)
                    ?? await userManager.FindByNameAsync(request.Username).ConfigureAwait(false);
                if (userByCreds == null || !await userManager.CheckPasswordAsync(userByCreds, request.Password).ConfigureAwait(false))
                {
                    _logger.LogWarning("Invalid username/email or password for user: {Username}", request.Username);
                    return null;
                }

                var useRememberMe = request.RememberMe == true;
                var (accessPw, minutesPw) = await _accessTokens.CreateAsync(userByCreds, useRememberMe).ConfigureAwait(false);
                var refreshPlain = GenerateOpaqueRefreshToken();
                await _refreshTokens.CreateAsync(userByCreds.Id, refreshPlain, useRememberMe).ConfigureAwait(false);
                return new OAuth2TokenResponse
                {
                    AccessToken = accessPw,
                    TokenType = "Bearer",
                    ExpiresIn = minutesPw * 60,
                    RefreshToken = refreshPlain,
                    Scope = request.Scope,
                };

            case "refresh_token":
                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    _logger.LogWarning("Refresh token grant type missing refresh token");
                    return null;
                }

                if (_accessTokens.IsValidAccessTokenMisusedAsRefresh(request.RefreshToken))
                {
                    _logger.LogWarning("Client sent a valid access JWT as refresh_token; rejected");
                    return null;
                }

                var redeem = await _refreshTokens.RedeemAndRotateAsync(request.RefreshToken).ConfigureAwait(false);
                if (redeem == null)
                {
                    _logger.LogWarning("Refresh token redeem failed (unknown, expired, or reused)");
                    return null;
                }

                var userFromRefresh = await userManager.FindByIdAsync(redeem.UserId).ConfigureAwait(false);
                if (userFromRefresh == null)
                {
                    _logger.LogWarning("Refresh token referred to missing user {UserId}", redeem.UserId);
                    return null;
                }

                var (accessRf, minutesRf) = await _accessTokens.CreateAsync(userFromRefresh, redeem.UseRememberMeAccessLifetime)
                    .ConfigureAwait(false);
                return new OAuth2TokenResponse
                {
                    AccessToken = accessRf,
                    TokenType = "Bearer",
                    ExpiresIn = minutesRf * 60,
                    RefreshToken = redeem.NewPlainRefreshToken,
                    Scope = request.Scope,
                };

            default:
                _logger.LogWarning("Unsupported grant type: {GrantType}", request.GrantType);
                return null;
        }
    }

    private static string GenerateOpaqueRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
