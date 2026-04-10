using System.Security.Claims;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Utils;

/// <summary>
/// Central checks for platform (multi-tenant admin UI) vs tenant scope. Keeps ACL rules out of scattered string compares.
/// </summary>
public static class PlatformAccessRules
{
    public static bool IsGlobalAdmin(ClaimsPrincipal user) =>
        user.IsInRole(UserRole.GlobalRoleNames.Admin) ||
        user.IsInRole(UserRole.GlobalRoleNames.SuperAdmin);

    public static bool IsGlobalSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole(UserRole.GlobalRoleNames.SuperAdmin);

    /// <summary>
    /// Admin SPA URL prefix (/admin/...) plus global Admin or SuperAdmin — full face directory, users, tenant CMS from platform UI.
    /// </summary>
    public static bool CanManageAllFaces(IFaceScopeContext faceScope, ClaimsPrincipal user) =>
        faceScope.IsAdminFaceScope && IsGlobalAdmin(user);

    /// <summary>
    /// Mutations to global schema tables (PageTypes, etc.) — same bar as platform operators (A15).
    /// </summary>
    public static bool CanMutateGlobalPageTypes(IFaceScopeContext faceScope, ClaimsPrincipal user) =>
        CanManageAllFaces(faceScope, user);
}
