using System.Text.Json;
using BeDemo.Api.Models.DTOs;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <inheritdoc />
public sealed class SearchElasticsearchProbe : ISearchElasticsearchProbe
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<SearchOptions> _options;
    private readonly ILogger<SearchElasticsearchProbe> _logger;

    public SearchElasticsearchProbe(
        IHttpClientFactory httpClientFactory,
        IOptions<SearchOptions> options,
        ILogger<SearchElasticsearchProbe> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SearchHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        if (!o.IsEnabled)
        {
            return new SearchHealthDto
            {
                Configured = false,
                Reachable = false,
                ClusterName = null,
                Message = "Search is not configured (set Search:ElasticsearchUri).",
            };
        }

        var baseUri = new Uri(o.ElasticsearchUri!.TrimEnd('/') + "/", UriKind.Absolute);
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.RequestTimeoutSeconds, 1, 120));

        using var request = new HttpRequestMessage(HttpMethod.Get, baseUri);

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SearchHealthDto
                {
                    Configured = true,
                    Reachable = false,
                    ClusterName = null,
                    Message = $"HTTP {(int)response.StatusCode}",
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var cluster = root.TryGetProperty("cluster_name", out var cn) ? cn.GetString() : null;
            return new SearchHealthDto
            {
                Configured = true,
                Reachable = true,
                ClusterName = cluster,
                Message = null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Elasticsearch probe failed");
            return new SearchHealthDto
            {
                Configured = true,
                Reachable = false,
                ClusterName = null,
                Message = ex.Message,
            };
        }
    }
}
