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

namespace LearningPlatform.Classroom.Features.HandRaises.LowerOwn;

internal static class LowerOwnEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/classroom/sessions/{sessionId:guid}/raise-hand", LowerOwnAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
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

}
