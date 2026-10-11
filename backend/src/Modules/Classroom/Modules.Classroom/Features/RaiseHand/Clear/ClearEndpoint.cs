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

namespace LearningPlatform.Classroom.Features.HandRaises.Clear;

internal static class ClearEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/raise-hands/{studentId:guid}/clear",
                ClearAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
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

}
