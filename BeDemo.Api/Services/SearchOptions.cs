namespace BeDemo.Api.Services;

/// <summary>
/// Optional Elasticsearch HTTP endpoint for search projections. When <see cref="ElasticsearchUri"/> is unset,
/// search-related features remain disabled and the API does not require a cluster at startup.
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>Base URI for the Elasticsearch HTTP API, e.g. <c>http://elasticsearch-dev:9200/</c>.</summary>
    public string? ElasticsearchUri { get; set; }

    public string IndexPrefix { get; set; } = "manyfaces";

    public int RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>Reserved for hosted Elastic / API key auth (wired with the official client in a later phase).</summary>
    public string? ApiKey { get; set; }

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ElasticsearchUri) &&
        Uri.TryCreate(ElasticsearchUri, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
