using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Optional Elasticsearch search infrastructure (health probe only in the first phase).
/// </summary>
[ApiController]
[Route("api/search")]
[Authorize]
public sealed class SearchController : ControllerBase
{
    private readonly ISearchElasticsearchProbe _probe;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchElasticsearchProbe probe, ILogger<SearchController> logger)
    {
        _probe = probe;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether Elasticsearch is configured and whether the root HTTP API responds.
    /// Anonymous callers may use the <b>public</b> face URL prefix (same pattern as <c>GET /api/Stats/public</c>).
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<ActionResult<SearchHealthDto>> GetHealth(CancellationToken cancellationToken)
    {
        var result = await _probe.GetHealthAsync(cancellationToken);
        if (result.Configured && !result.Reachable)
        {
            _logger.LogWarning("Search health: Elasticsearch unreachable: {Message}", result.Message);
        }

        return Ok(result);
    }
}
