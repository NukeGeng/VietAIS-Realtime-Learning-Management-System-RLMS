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

namespace LearningPlatform.Classroom.Features.LiveSessions.JoinSession;

internal static class JoinSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/join", JoinSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }


    private static async Task<IResult> JoinSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        LivePresenceStore presence,
        LiveSessionSnapshotCache snapshotCache,
        IConfiguration configuration,
        IMessageBus messageBus,
        ILogger<LiveSessionHub> logger,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await snapshotCache.GetAsync(actor!.OrganizationId, sessionId,
            token => session.LoadAsync<LiveSession>(sessionId, token), cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.Status == LiveSessionStatus.Ended)
        {
            return Results.Conflict(new { error = "Live session has ended." });
        }

        if (liveSession.Status != LiveSessionStatus.Started)
        {
            return Results.Conflict(new { error = "The live session has not started." });
        }

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var moderation = await session.LoadAsync<LiveSessionParticipantModeration>(
            ClassroomConstants.ModerationId(sessionId, actor.UserId), cancellationToken);
        if (moderation?.IsBlocked == true)
        {
            return Results.Forbid();
        }

        var authenticatedActor = actor!;
        var joinedAt = DateTimeOffset.UtcNow;
        var presenceRegistered = await presence.MarkJoinedAsync(authenticatedActor.OrganizationId, sessionId,
            authenticatedActor.UserId,
            cancellationToken);
        if (await academics.IsStudentEnrolledAsync(authenticatedActor.OrganizationId,
                liveSession.LearningClassId, authenticatedActor.UserId, cancellationToken))
        {
            await AttendanceMessagePublisher.PublishParticipantJoinedAsync(messageBus, logger,
                authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId, joinedAt);
        }
        var mediaServerUrl = configuration["Media:ServerUrl"] ?? "";
        var accessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return Results.Ok(new LiveSessionJoinResponse(LiveSessionResponse.From(liveSession),
            new MediaAccessResponse(mediaServerUrl, liveSession.MediaRoomId, accessToken),
            presenceRegistered));
    }

}
