using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>Validates OAuth2 confidential client credentials against <see cref="OAuthClient"/> rows (O1).</summary>
public interface IOAuthClientValidator
{
    Task<bool> ValidateAsync(string? clientId, string? clientSecret);
}

public sealed class OAuthClientValidator : IOAuthClientValidator
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<OAuthClient> _oauthClientHasher;
    private readonly ILogger<OAuthClientValidator> _logger;

    public OAuthClientValidator(
        ApplicationDbContext db,
        IPasswordHasher<OAuthClient> oauthClientHasher,
        ILogger<OAuthClientValidator> logger)
    {
        _db = db;
        _oauthClientHasher = oauthClientHasher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateAsync(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return false;

        var row = await _db.OAuthClients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive)
            .ConfigureAwait(false);
        if (row == null)
        {
            _logger.LogDebug("OAuth client not found or inactive: {ClientId}", clientId);
            return false;
        }

        var r = _oauthClientHasher.VerifyHashedPassword(row, row.SecretHash, clientSecret);
        return r is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
