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

namespace LearningPlatform.Classroom.Features.LiveSessions.CreateSession;

internal static class CreateSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/classes/{classId:guid}/sessions", CreateSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
    }


    private static async Task<IResult> CreateSessionAsync(
        Guid classId,
        CreateLiveSessionRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot create a live session." });
        }

        if (request.LateAfterMinutes is < 0 or > 240)
        {
            return Results.BadRequest(new { error = "LateAfterMinutes must be between 0 and 240." });
        }

        var teacherId = request.TeacherId ?? actor.UserId;
        var canManageClass = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
            StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
        if (!canManageClass && !await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, teacherId,
                cancellationToken))
        {
            return Results.BadRequest(new { error = "The session teacher must be assigned to the class." });
        }

        if (request.ClassScheduleId is { } scheduleId)
        {
            var schedule = await academics.FindScheduleAsync(actor.OrganizationId, classId, scheduleId,
                cancellationToken);
            if (schedule is null || schedule.Status is ScheduleStatus.Cancelled or ScheduleStatus.Completed)
            {
                return Results.Conflict(new { error = "The selected schedule is not available." });
            }

            if (schedule.TeacherId != teacherId)
            {
                return Results.BadRequest(new { error = "The session teacher must match the schedule teacher." });
            }
        }

        var now = DateTimeOffset.UtcNow;
        var liveSession = new LiveSession
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            LearningClassId = classId,
            ClassScheduleId = request.ClassScheduleId,
            TeacherId = teacherId,
            Status = LiveSessionStatus.Scheduled,
            LateAfterMinutes = request.LateAfterMinutes ?? 15,
            MediaRoomId = LiveSessionRules.RequireMediaRoomId($"rlms-{Guid.NewGuid():N}"),
            CreatedAt = now,
            UpdatedAt = now
        };
        session.Insert(liveSession);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/classroom/sessions/{liveSession.Id:D}",
            LiveSessionResponse.From(liveSession));
    }

}
