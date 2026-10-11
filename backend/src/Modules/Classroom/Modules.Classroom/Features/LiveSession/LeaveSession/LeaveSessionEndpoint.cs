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

namespace LearningPlatform.Classroom.Features.LiveSessions.LeaveSession;

internal static class LeaveSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/leave", LeaveSessionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }


    private static async Task<IResult> LeaveSessionAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        LivePresenceStore presence,
        LiveSessionSnapshotCache snapshotCache,
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

        if (!await CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var authenticatedActor = actor!;
        var leftAt = DateTimeOffset.UtcNow;
        await presence.RemoveAsync(authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId,
            cancellationToken);
        await AttendanceMessagePublisher.PublishParticipantLeftAsync(messageBus, logger,
            authenticatedActor.OrganizationId, sessionId, authenticatedActor.UserId, leftAt);
        return Results.NoContent();
    }

}
