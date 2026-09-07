using LearningPlatform.Academics.Authorization;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;

namespace LearningPlatform.Classroom.Api;

/// <summary>
/// Shared resource authorization for live-session HTTP endpoints, SignalR and
/// communication features. The caller must have already passed the endpoint
/// permission policy; this check enforces tenant and class membership ownership.
/// </summary>
public static class LiveSessionAuthorization
{
    public static async Task<bool> CanJoinAsync(
        AuthenticatedActor actor,
        LiveSession liveSession,
        IAcademicDirectory academics,
        CancellationToken cancellationToken = default)
    {
        if (actor.OrganizationId != liveSession.OrganizationId)
        {
            return false;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, liveSession.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return true;
        }

        return await academics.IsTeacherAssignedAsync(actor.OrganizationId, liveSession.LearningClassId,
                   actor.UserId, cancellationToken) ||
               await academics.IsStudentEnrolledAsync(actor.OrganizationId, liveSession.LearningClassId,
                   actor.UserId, cancellationToken);
    }

    public static async Task<bool> CanManageAsync(
        AuthenticatedActor actor,
        LiveSession liveSession,
        IAcademicDirectory academics,
        CancellationToken cancellationToken = default)
    {
        if (actor.OrganizationId != liveSession.OrganizationId)
        {
            return false;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal))
        {
            return true;
        }

        return actor.Permissions.Contains(PermissionCatalog.ManageLiveSessions, StringComparer.Ordinal) &&
            (await academics.IsClassCreatorAsync(actor.OrganizationId, liveSession.LearningClassId,
                 actor.UserId, cancellationToken) ||
             await academics.IsTeacherAssignedAsync(actor.OrganizationId, liveSession.LearningClassId,
                 actor.UserId, cancellationToken));
    }

    public static async Task<bool> CanManageAttendanceAsync(
        AuthenticatedActor actor,
        LiveSession liveSession,
        IAcademicDirectory academics,
        CancellationToken cancellationToken = default)
    {
        if (actor.OrganizationId != liveSession.OrganizationId)
        {
            return false;
        }

        if (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal))
        {
            return true;
        }

        return actor.Permissions.Contains(PermissionCatalog.ManageAttendance, StringComparer.Ordinal) &&
            (await academics.IsClassCreatorAsync(actor.OrganizationId, liveSession.LearningClassId,
                 actor.UserId, cancellationToken) ||
             await academics.IsTeacherAssignedAsync(actor.OrganizationId, liveSession.LearningClassId,
                 actor.UserId, cancellationToken));
    }
}
