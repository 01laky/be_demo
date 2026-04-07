using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Utils;

public static class StoryViewerRules
{
    /// <summary>Stories lists are shown only to users with a non-host face role in that face.</summary>
    public static async Task<bool> ViewerIsActiveNonHostInFaceAsync(
        ApplicationDbContext context,
        string viewerUserId,
        int faceId,
        CancellationToken cancellationToken = default)
    {
        var ufr = await context.UserFaceRoles
            .AsNoTracking()
            .Include(x => x.UserRole)
            .FirstOrDefaultAsync(x => x.UserId == viewerUserId && x.FaceId == faceId, cancellationToken);

        if (ufr?.UserRole == null)
            return false;

        return FaceRoleParticipation.IsActiveForFaceRoleName(ufr.UserRole.Name);
    }
}
