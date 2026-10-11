using LearningPlatform.Communication.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Communication.Api.AnnouncementEndpoints;

namespace LearningPlatform.Communication.Features.Announcements.MarkNotificationRead;

internal static class MarkNotificationReadEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/communication/notifications/{notificationId:guid}/read",
                MarkNotificationReadAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadNotifications);
    }


    private static async Task<IResult> MarkNotificationReadAsync(Guid notificationId,
        HttpContext context, IDocumentSession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var notification = await session.LoadAsync<Notification>(notificationId, cancellationToken);
        if (notification is null || notification.OrganizationId != actor!.OrganizationId ||
            notification.UserId != actor.UserId)
        {
            return Results.NotFound();
        }

        notification.ReadAt ??= DateTimeOffset.UtcNow;
        session.Store(notification);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(NotificationResponse.From(notification));
    }

}
