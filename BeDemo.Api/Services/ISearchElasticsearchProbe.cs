using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Minimal connectivity probe against the configured Elasticsearch HTTP API (no indexing in this phase).
/// </summary>
public interface ISearchElasticsearchProbe
{
    Task<SearchHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
}
