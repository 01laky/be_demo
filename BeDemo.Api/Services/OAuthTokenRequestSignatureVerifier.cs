using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Validates optional ECDSA body signatures on the token endpoint (legacy path; O4 rejects new usage from clients).
/// </summary>
public interface IOAuthTokenRequestSignatureVerifier
{
    bool IsSignatureValid(OAuth2TokenRequest request);
}

public sealed class OAuthTokenRequestSignatureVerifier : IOAuthTokenRequestSignatureVerifier
{
    private readonly IECDSAKeyService _keyService;
    private readonly ILogger<OAuthTokenRequestSignatureVerifier> _logger;

    public OAuthTokenRequestSignatureVerifier(
        IECDSAKeyService keyService,
        ILogger<OAuthTokenRequestSignatureVerifier> logger)
    {
        _keyService = keyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsSignatureValid(OAuth2TokenRequest request)
    {
        if (string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.SignatureAlgorithm))
        {
            _logger.LogWarning("Request missing signature or algorithm");
            return false;
        }

        if (request.SignatureAlgorithm != "ES512")
        {
            _logger.LogWarning("Unsupported signature algorithm: {Algorithm}", request.SignatureAlgorithm);
            return false;
        }

        try
        {
            var message = CreateSignatureMessage(request);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var signatureBytes = Convert.FromBase64String(request.Signature);
            var validationKey = _keyService.GetValidationKey();
            var ecdsa = validationKey.ECDsa ?? throw new InvalidOperationException("ECDSA key not available");
            return ecdsa.VerifyData(messageBytes, signatureBytes, HashAlgorithmName.SHA512);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating request signature");
            return false;
        }
    }

    private static string CreateSignatureMessage(OAuth2TokenRequest request)
    {
        var parts = new List<string>
        {
            $"grant_type={request.GrantType}",
            $"client_id={request.ClientId ?? ""}",
            $"username={request.Username ?? ""}",
            $"scope={request.Scope ?? ""}",
            $"timestamp={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}",
        };
        return string.Join("&", parts);
    }
}
