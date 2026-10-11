using LearningPlatform.Classroom.Api;
using System.Security.Cryptography;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Classroom.Api.LiveSessionEndpoints;

namespace LearningPlatform.Classroom.Features.LiveSessions.ListClassSessions;

internal static class ListClassSessionsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/classes/{classId:guid}/sessions", ListClassSessionsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }


    private static async Task<IResult> ListClassSessionsAsync(
        Guid classId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        bool? includeEnded,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var canAccessClass = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
        if (!canAccessClass)
        {
            return Results.Forbid();
        }

        var canReadAttendanceHistory = includeEnded == true &&
            actor.Permissions.Contains(PermissionCatalog.ManageAttendance, StringComparer.Ordinal);
        var sessionsQuery = session.Query<LiveSession>()
            .Where(value => value.OrganizationId == actor.OrganizationId &&
                            value.LearningClassId == classId &&
                            (value.Status == LiveSessionStatus.Scheduled ||
                             value.Status == LiveSessionStatus.Started ||
                             (canReadAttendanceHistory && value.Status == LiveSessionStatus.Ended)));
        if (!actor.Permissions.Contains(PermissionCatalog.ManageLiveSessions, StringComparer.Ordinal) &&
            !canReadAttendanceHistory)
        {
            sessionsQuery = sessionsQuery.Where(value => value.Status == LiveSessionStatus.Started);
        }

        var sessions = await sessionsQuery
            .OrderByDescending(value => value.Status)
            .ThenByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(sessions.Select(LiveSessionResponse.From));
    }

}
