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

namespace LearningPlatform.Communication.Features.Announcements.ListNotifications;

internal static class ListNotificationsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/communication/notifications", ListNotificationsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadNotifications);
    }


    private static async Task<IResult> ListNotificationsAsync(bool? unreadOnly,
        HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var query = session.Query<Notification>()
            .Where(notification => notification.OrganizationId == actor!.OrganizationId &&
                notification.UserId == actor.UserId);
        var notifications = await query.OrderByDescending(notification => notification.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        // Keep the read model resilient to legacy duplicate deliveries. New
        // transitions are idempotent at write time; this also prevents old
        // duplicate documents from resurfacing in the user's inbox.
        var uniqueNotifications = notifications
            .GroupBy(notification => new
            {
                notification.Type,
                notification.SourceId,
                Title = NotificationResponse.LocalizedTitleForGrouping(notification),
                Body = NotificationResponse.LocalizedBodyForGrouping(notification),
                // Source-backed schedule/announcement events can be replayed
                // with a new delivery id. Source-less messages still need
                // their occurrence timestamp to avoid collapsing separate
                // notifications that happen to share the same text.
                OccurredAt = notification.SourceId is null ? notification.CreatedAt : DateTimeOffset.MinValue
            })
            .Select(group => group
                .OrderByDescending(notification => notification.ReadAt is null)
                .ThenByDescending(notification => notification.CreatedAt)
                .ThenBy(notification => notification.Id)
                .First())
            .Where(notification => unreadOnly != true || notification.ReadAt is null)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100);
        return Results.Ok(uniqueNotifications.Select(NotificationResponse.From));
    }

}
