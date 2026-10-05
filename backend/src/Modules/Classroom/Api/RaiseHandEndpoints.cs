using LearningPlatform.Academics.Authorization;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Api;

public static class RaiseHandEndpoints
{
    public static void MapRaiseHandEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/raise-hand", RaiseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapDelete("/api/classroom/sessions/{sessionId:guid}/raise-hand", LowerOwnAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/raise-hands", ListAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/raise-hands/{studentId:guid}/clear",
                ClearAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
    }

    private static async Task<IResult> RaiseAsync(
        Guid sessionId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IHubContext<LiveSessionHub> hub,
        ILogger<LiveSessionHub> logger,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.Status != LiveSessionStatus.Started)
        {
            return Results.Conflict(new { error = "The live session is not active." });
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, liveSession.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        var id = ClassroomConstants.RaiseHandId(sessionId, actor.UserId);
        var hand = await session.LoadAsync<RaiseHand>(id, cancellationToken);
        if (hand is null)
        {
            hand = new RaiseHand
            {
                Id = id,
                OrganizationId = actor.OrganizationId,
                LiveSessionId = sessionId,
                StudentId = actor.UserId,
                Status = RaiseHandStatus.Raised,
                RaisedAt = now,
                UpdatedAt = now
            };
            session.Insert(hand);
        }
        else if (hand.Status != RaiseHandStatus.Raised)
        {
            hand.Status = RaiseHandStatus.Raised;
            hand.RaisedAt = now;
            hand.LoweredAt = null;
            hand.ClearedBy = null;
            hand.UpdatedAt = now;
            session.Update(hand);
        }

        await session.SaveChangesAsync(cancellationToken);
        var response = RaiseHandResponse.From(hand);
        await BroadcastAsync(hub, logger, actor.OrganizationId, sessionId, "hand-raised", response);
        return Results.Ok(response);
    }

    private static async Task<IResult> LowerOwnAsync(
        Guid sessionId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IHubContext<LiveSessionHub> hub,
        ILogger<LiveSessionHub> logger,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, liveSession.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var id = ClassroomConstants.RaiseHandId(sessionId, actor.UserId);
        var hand = await session.LoadAsync<RaiseHand>(id, cancellationToken);
        if (hand is null || hand.Status == RaiseHandStatus.Lowered)
        {
            return Results.NoContent();
        }

        var now = DateTimeOffset.UtcNow;
        hand.Status = RaiseHandStatus.Lowered;
        hand.LoweredAt = now;
        hand.UpdatedAt = now;
        session.Update(hand);
        await session.SaveChangesAsync(cancellationToken);
        await BroadcastAsync(hub, logger, actor.OrganizationId, sessionId, "hand-lowered",
            RaiseHandResponse.From(hand));
        return Results.NoContent();
    }

    private static async Task<IResult> ListAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await LiveSessionAuthorization.CanManageAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var hands = await session.Query<RaiseHand>()
            .Where(hand => hand.LiveSessionId == sessionId && hand.Status == RaiseHandStatus.Raised)
            .OrderBy(hand => hand.RaisedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(hands.Select(RaiseHandResponse.From).ToArray());
    }

    private static async Task<IResult> ClearAsync(
        Guid sessionId,
        Guid studentId,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IHubContext<LiveSessionHub> hub,
        ILogger<LiveSessionHub> logger,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (!await LiveSessionAuthorization.CanManageAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var hand = await session.LoadAsync<RaiseHand>(ClassroomConstants.RaiseHandId(sessionId, studentId),
            cancellationToken);
        if (hand is null || hand.Status == RaiseHandStatus.Lowered)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        hand.Status = RaiseHandStatus.Lowered;
        hand.LoweredAt = now;
        hand.ClearedBy = actor!.UserId;
        hand.UpdatedAt = now;
        session.Update(hand);
        await session.SaveChangesAsync(cancellationToken);
        await BroadcastAsync(hub, logger, actor.OrganizationId, sessionId, "hand-lowered",
            RaiseHandResponse.From(hand));
        return Results.NoContent();
    }

    private static async Task BroadcastAsync(
        IHubContext<LiveSessionHub> hub,
        ILogger logger,
        Guid organizationId,
        Guid sessionId,
        string eventName,
        RaiseHandResponse response)
    {
        try
        {
            await hub.Clients.Group(LiveSessionHub.Group(organizationId, sessionId))
                .SendAsync(eventName, response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Hand signals are useful realtime hints; durable state remains correct
            // if the SignalR channel is temporarily unavailable.
            logger.LogWarning(exception, "Unable to broadcast raise-hand update for {SessionId}.", sessionId);
        }
    }
}

public sealed record RaiseHandResponse(
    Guid Id,
    Guid LiveSessionId,
    Guid StudentId,
    RaiseHandStatus Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? LoweredAt,
    Guid? ClearedBy)
{
    public static RaiseHandResponse From(RaiseHand hand) => new(hand.Id, hand.LiveSessionId, hand.StudentId,
        hand.Status, hand.RaisedAt, hand.LoweredAt, hand.ClearedBy);
}
