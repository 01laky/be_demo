namespace BeDemo.Api.Models.Requests.OAuth;

/// <summary>GET /api/oauth2/register/prefill?hash=</summary>
public sealed class RegisterPrefillQuery
{
    public string? Hash { get; set; }
}

/// <summary>GET /api/admin/registration-invites list query.</summary>
public sealed class AdminInviteListQuery
{
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
}

/// <summary>GET /api/localization/{app} bundle query.</summary>
public sealed class LocalizationBundleQuery
{
    public string? V { get; set; }
}
