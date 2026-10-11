using LearningPlatform.Communication.Api;
using LearningPlatform.Classroom.Api;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using static LearningPlatform.Communication.Api.ChatEndpoints;

namespace LearningPlatform.Communication.Features.Chat.Send;

internal static class SendEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/classroom/sessions/{sessionId:guid}/messages", SendAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.JoinLiveSessions);
    }


    private static async Task<IResult> SendAsync(
        Guid sessionId,
        SendChatMessageRequest request,
        HttpContext context,
        IDocumentSession session,
        LearningPlatform.Academics.Authorization.IAcademicDirectory academics,
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
            return Results.Conflict(new { error = "Chat is available only while the live session is active." });
        }

        if (!await LiveSessionAuthorization.CanJoinAsync(actor!, liveSession, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                LiveSessionId = sessionId,
                SenderId = actor.UserId,
                Body = ChatRules.RequireMessage(request.Body),
                SentAt = now
            };
            session.Insert(message);
            await session.SaveChangesAsync(cancellationToken);

            var response = ChatMessageResponse.From(message);
            try
            {
                await hub.Clients.Group(LiveSessionHub.Group(actor.OrganizationId, sessionId))
                    .SendAsync("chat-message", response, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Persistence is the source of truth. A SignalR outage only delays
                // the live notification; clients can reload the message snapshot.
                logger.LogWarning(exception, "Unable to broadcast chat message for {SessionId}.", sessionId);
            }

            return Results.Created($"/api/classroom/sessions/{sessionId:D}/messages/{message.Id:D}", response);
        }
        catch (ChatValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
