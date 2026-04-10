/*
 * OAuth2Service.cs - Service for OAuth2 authentication and JWT token generation
 * 
 * This service implements OAuth2 Authorization Code flow with support for:
 * - Password grant type (Resource Owner Password Credentials)
 * - Refresh token grant type
 * - ECDSA signed JWT tokens (ES512 algorithm)
 * - Client credentials validation
 * - Request signature validation using ECDSA
 * 
 * JWT tokens contain claims (statements) about the user:
 * - NameIdentifier (User ID)
 * - Name (Username)
 * - Email
 * - GivenName (First Name)
 * - Surname (Last Name)
 * - Jti (JWT ID - unique token identifier)
 * - Iat (Issued At - token creation time)
 */

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Data;
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
    /// Validates ECDSA request signature
    /// </summary>
    bool ValidateRequestSignature(OAuth2TokenRequest request);

    /// <summary>
    /// Validates client credentials (client_id and client_secret)
    /// </summary>
    Task<bool> ValidateClientAsync(string? clientId, string? clientSecret);
}

/// <summary>
/// OAuth2 service implementation with ECDSA signing
/// </summary>
public class OAuth2Service : IOAuth2Service
{
    private readonly IECDSAKeyService _keyService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OAuth2Service> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IOAuthRefreshTokenStore _refreshTokens;

    public OAuth2Service(
        IECDSAKeyService keyService,
        IConfiguration configuration,
        ILogger<OAuth2Service> logger,
        ApplicationDbContext db,
        IOAuthRefreshTokenStore refreshTokens)
    {
        _keyService = keyService;
        _configuration = configuration;
        _logger = logger;
        _db = db;
        _refreshTokens = refreshTokens;
    }

    /// <summary>
    /// Password grant: validate credentials, persist refresh token (hash only), return JWT + opaque refresh.
    /// Refresh grant: reject valid access JWTs misused as refresh; rotate stored refresh (single-use); return new pair.
    /// </summary>
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
                    ?? await userManager.FindByNameAsync(request.Username);
                if (userByCreds == null || !await userManager.CheckPasswordAsync(userByCreds, request.Password))
                {
                    _logger.LogWarning("Invalid username/email or password for user: {Username}", request.Username);
                    return null;
                }

                // Persist refresh before returning plaintext to client (A17).
                var useRememberMe = request.RememberMe == true;
                var (accessPw, minutesPw) = await BuildAccessJwtAsync(userByCreds, useRememberMe);
                var refreshPlain = GenerateRefreshToken();
                await _refreshTokens.CreateAsync(userByCreds.Id, refreshPlain, useRememberMe);
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

                // Opaque store is authoritative; still block a *currently valid* access JWT from being used here.
                if (IsValidAccessTokenMisusedAsRefresh(request.RefreshToken))
                {
                    _logger.LogWarning("Client sent a valid access JWT as refresh_token; rejected");
                    return null;
                }

                var redeem = await _refreshTokens.RedeemAndRotateAsync(request.RefreshToken);
                if (redeem == null)
                {
                    _logger.LogWarning("Refresh token redeem failed (unknown, expired, or reused)");
                    return null;
                }

                var userFromRefresh = await userManager.FindByIdAsync(redeem.UserId);
                if (userFromRefresh == null)
                {
                    _logger.LogWarning("Refresh token referred to missing user {UserId}", redeem.UserId);
                    return null;
                }

                var (accessRf, minutesRf) = await BuildAccessJwtAsync(userFromRefresh, redeem.UseRememberMeAccessLifetime);
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

    /// <summary>
    /// Builds signed access JWT and picks TTL: session vs remember-me (aligned with A2 / JwtBearer ValidateLifetime).
    /// Global role claim is loaded from DB so admin promotion applies on refresh without requiring password re-entry.
    /// </summary>
    private async Task<(string AccessToken, int ExpiresInMinutes)> BuildAccessJwtAsync(ApplicationUser user, bool useRememberMeAccessLifetime)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrEmpty(user.FirstName))
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        if (!string.IsNullOrEmpty(user.LastName))
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

        var globalRoleName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => u.UserRole.Name)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(globalRoleName))
            claims.Add(new Claim(ClaimTypes.Role, globalRoleName));

        var signingKey = _keyService.GetSigningKey();
        var sessionMinutes = _configuration.GetValue("Jwt:ExpiresInMinutes", 60);
        var rememberMinutes = _configuration.GetValue("Jwt:ExpiresInMinutesRememberMe", 10080);
        var expiresInMinutes = useRememberMeAccessLifetime ? rememberMinutes : sessionMinutes;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes),
            Issuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",
            Audience = _configuration["Jwt:Audience"] ?? "BeDemoApi",
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.EcdsaSha512),
            Claims = new Dictionary<string, object> { { "key_id", _keyService.GetKeyId() } },
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);
        return (accessToken, expiresInMinutes);
    }

    /// <summary>
    /// True when the string is a syntactic JWT that still validates as our current access token — must not be accepted as refresh.
    /// </summary>
    private bool IsValidAccessTokenMisusedAsRefresh(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return false;
        try
        {
            handler.ValidateToken(token, GetTokenValidationParameters(), out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates ECDSA request signature
    /// Request can be signed using ECDSA algorithm to ensure integrity
    /// </summary>
    public bool ValidateRequestSignature(OAuth2TokenRequest request)
    {
        // If request doesn't contain signature or algorithm, validation fails
        if (string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.SignatureAlgorithm))
        {
            _logger.LogWarning("Request missing signature or algorithm");
            return false;
        }

        // We only support ES512 algorithm (ECDSA with P-521 and SHA-512)
        if (request.SignatureAlgorithm != "ES512")
        {
            _logger.LogWarning("Unsupported signature algorithm: {Algorithm}", request.SignatureAlgorithm);
            return false;
        }

        try
        {
            // Creates canonical message from request parameters
            // This message must be the same as the one that was signed by client
            var message = CreateSignatureMessage(request);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            // Decodes Base64 signature
            var signatureBytes = Convert.FromBase64String(request.Signature);

            // Gets validation key (in this case it's the same key as signing key)
            var validationKey = _keyService.GetValidationKey();
            var ecdsa = validationKey.ECDsa ?? throw new InvalidOperationException("ECDSA key not available");

            // Validates signature using ECDSA VerifyData
            // Returns true if signature is valid (message was signed with correct private key)
            return ecdsa.VerifyData(messageBytes, signatureBytes, HashAlgorithmName.SHA512);
        }
        catch (Exception ex)
        {
            // If validation fails (e.g., invalid Base64, bad message, etc.), logs error and returns false
            _logger.LogError(ex, "Error validating request signature");
            return false;
        }
    }

    /// <summary>
    /// Validates client credentials (client_id and client_secret)
    /// In production, these should be stored in database or other secure storage
    /// </summary>
    public Task<bool> ValidateClientAsync(string? clientId, string? clientSecret)
    {
        // Loads valid client credentials from configuration
        // In production, they should be stored in database or use OAuth2 Client Store
        var validClientId = _configuration["OAuth2:ClientId"] ?? "be-demo-client";
        var validClientSecret = _configuration["OAuth2:ClientSecret"] ?? "be-demo-secret-very-strong-key";

        // Validates that client_id and client_secret are provided and match valid credentials
        var isValid = !string.IsNullOrEmpty(clientId) &&
                     !string.IsNullOrEmpty(clientSecret) &&
                     clientId == validClientId &&
                     clientSecret == validClientSecret;

        return Task.FromResult(isValid);
    }

    /// <summary>
    /// Creates canonical message from request parameters for signing
    /// Message must always be in the same format for validation to work correctly
    /// </summary>
    private string CreateSignatureMessage(OAuth2TokenRequest request)
    {
        // Creates list of parameters in canonical format
        // Format: key=value&key=value&...
        var parts = new List<string>
        {
            $"grant_type={request.GrantType}",
            $"client_id={request.ClientId ?? ""}",
            $"username={request.Username ?? ""}",
            $"scope={request.Scope ?? ""}",
            $"timestamp={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"  // Timestamp ensures each message is unique
        };

        // Joins all parameters using &
        return string.Join("&", parts);
    }

    /// <summary>
    /// Generates random refresh token
    /// Refresh token is a long-term token used to refresh access token
    /// </summary>
    private string GenerateRefreshToken()
    {
        // Generates 64 random bytes (512 bits)
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        // Converts to Base64 string for easy transmission
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Creates TokenValidationParameters for JWT token validation
    /// These parameters are used when validating refresh tokens
    /// </summary>
    private TokenValidationParameters GetTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,                    // Validates signing key
            IssuerSigningKey = _keyService.GetValidationKey(),   // Key for validation
            ValidateIssuer = true,                              // Validates issuer
            ValidIssuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",
            ValidateAudience = true,                            // Validates audience
            ValidAudience = _configuration["Jwt:Audience"] ?? "BeDemoApi",
            ValidateLifetime = true,                            // Validates expiration
            ClockSkew = TimeSpan.Zero                           // No tolerance for time skew
        };
    }
}
