using LearningPlatform.Classroom.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static LearningPlatform.Classroom.Api.RaiseHandEndpoints;

namespace LearningPlatform.Classroom.Features.HandRaises.Raise;

internal static class RaiseEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/raise-hand", RaiseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
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

}
