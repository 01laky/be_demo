using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;

namespace BeDemo.Api.Services;

/// <summary>Builds signed ES512 access JWTs and detects access JWTs misused as refresh tokens.</summary>
public interface IOAuthAccessTokenFactory
{
    Task<(string AccessToken, int ExpiresInMinutes)> CreateAsync(ApplicationUser user, bool useRememberMeAccessLifetime);

    /// <summary>True when the string validates as a current access JWT — must not be accepted as <c>refresh_token</c>.</summary>
    bool IsValidAccessTokenMisusedAsRefresh(string token);
}

public sealed class OAuthAccessTokenFactory : IOAuthAccessTokenFactory
{
    private readonly IECDSAKeyService _keyService;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OAuthAccessTokenFactory> _logger;

    public OAuthAccessTokenFactory(
        IECDSAKeyService keyService,
        IConfiguration configuration,
        ApplicationDbContext db,
        ILogger<OAuthAccessTokenFactory> logger)
    {
        _keyService = keyService;
        _configuration = configuration;
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(string AccessToken, int ExpiresInMinutes)> CreateAsync(
        ApplicationUser user,
        bool useRememberMeAccessLifetime)
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

        var row = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => new { u.AccessTokenVersion, GlobalRoleName = u.UserRole.Name })
            .FirstAsync()
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(row.GlobalRoleName))
            claims.Add(new Claim(ClaimTypes.Role, row.GlobalRoleName));
        claims.Add(new Claim(BeDemoClaimTypes.AccessTokenVersion, row.AccessTokenVersion.ToString(), ClaimValueTypes.Integer32));

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

    /// <inheritdoc />
    public bool IsValidAccessTokenMisusedAsRefresh(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return false;
        try
        {
            handler.ValidateToken(token, GetTokenValidationParameters(), out _);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "String is not a valid access JWT (expected for opaque refresh)");
            return false;
        }
    }

    private TokenValidationParameters GetTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = _keyService.GetIssuerSigningKeys().ToList(),
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"] ?? "BeDemoApi",
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"] ?? "BeDemoApi",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha512 },
        };
    }
}
