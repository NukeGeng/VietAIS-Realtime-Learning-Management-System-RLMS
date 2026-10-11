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
        LearningPlatform.Classroom.Features.HandRaises.Raise.RaiseEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.HandRaises.LowerOwn.LowerOwnEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.HandRaises.List.ListEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.HandRaises.Clear.ClearEndpoint.Map(endpoints);
    }

    internal static async Task BroadcastAsync(
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
