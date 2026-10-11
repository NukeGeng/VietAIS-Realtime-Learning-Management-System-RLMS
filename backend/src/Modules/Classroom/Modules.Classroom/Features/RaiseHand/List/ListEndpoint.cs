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

namespace LearningPlatform.Classroom.Features.HandRaises.List;

internal static class ListEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/raise-hands", ListAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLiveSessions);
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

}
