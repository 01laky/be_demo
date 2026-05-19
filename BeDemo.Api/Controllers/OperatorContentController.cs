using System.Security.Claims;
using BeDemo.Api.Models.Requests.OperatorContent;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin operator content actions (album hard-delete shared by Remove + Delete album UI).</summary>
[ApiController]
[Route("api/operator-content")]
[Authorize]
public sealed class OperatorContentController : ControllerBase
{
    private readonly IAccessEvaluator _access;
    private readonly IOperatorAlbumManagementService _albums;

    public OperatorContentController(IAccessEvaluator access, IOperatorAlbumManagementService albums)
    {
        _access = access;
        _albums = albums;
    }

    private string? OperatorUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool RequireSuperAdmin() => _access.IsGlobalSuperAdmin(User);

    /// <summary>Hard-delete album (toolbar Remove and Delete album both use this).</summary>
    [HttpPost("albums/{id:int}/delete")]
    public async Task<IActionResult> HardDeleteAlbum(
        int id,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        await _albums.HardDeleteAlbumAsync(
            OperatorUserId,
            id,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return NoContent();
    }

    /// <summary>Delete one album media item; album row remains.</summary>
    [HttpPost("albums/{albumId:int}/media/{mediaId:int}/delete")]
    public async Task<IActionResult> DeleteAlbumMedia(
        int albumId,
        int mediaId,
        [FromBody] OperatorAlbumDeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!RequireSuperAdmin())
            return Forbid();
        if (string.IsNullOrEmpty(OperatorUserId))
            return Unauthorized();

        var ok = await _albums.DeleteAlbumMediaAsync(
            OperatorUserId,
            albumId,
            mediaId,
            request.FaceId,
            request.Reason,
            request.UserMessage,
            cancellationToken);

        return ok ? NoContent() : NotFound(new { error = "Album or media not found" });
    }
}
