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

namespace LearningPlatform.Communication.Api;

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Communication.Features.Announcements.ListAnnouncements.ListAnnouncementsEndpoint.Map(endpoints);
        LearningPlatform.Communication.Features.Announcements.CreateAnnouncement.CreateAnnouncementEndpoint.Map(endpoints);
        LearningPlatform.Communication.Features.Announcements.GetAnnouncement.GetAnnouncementEndpoint.Map(endpoints);
        LearningPlatform.Communication.Features.Announcements.PublishAnnouncement.PublishAnnouncementEndpoint.Map(endpoints);
        LearningPlatform.Communication.Features.Announcements.ArchiveAnnouncement.ArchiveAnnouncementEndpoint.Map(endpoints);
        LearningPlatform.Communication.Features.Announcements.ListNotifications.ListNotificationsEndpoint.Map(endpoints);
        LearningPlatform.Communication.Features.Announcements.MarkNotificationRead.MarkNotificationReadEndpoint.Map(endpoints);
    }

    internal static async Task<IResult> ChangeAnnouncementStatusAsync(Guid announcementId,
        AnnouncementStatus target, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var announcement = await session.LoadAsync<Announcement>(announcementId, cancellationToken);
        if (announcement is null)
        {
            return Results.NotFound();
        }

        if (announcement.OrganizationId != actor!.OrganizationId ||
            !await CanManageClassAsync(actor, announcement.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var valid = (announcement.Status, target) switch
        {
            (AnnouncementStatus.Draft, AnnouncementStatus.Published) => true,
            (AnnouncementStatus.Published, AnnouncementStatus.Archived) => true,
            _ => false
        };
        if (!valid)
        {
            return Results.Conflict(new { error = "The announcement cannot make that state transition." });
        }

        var now = DateTimeOffset.UtcNow;
        announcement.Status = target;
        announcement.PublishedAt = target == AnnouncementStatus.Published
            ? now
            : announcement.PublishedAt;
        announcement.UpdatedAt = now;
        session.Store(announcement);
        await session.SaveChangesAsync(cancellationToken);

        if (target == AnnouncementStatus.Published)
        {
            try
            {
                await messageBus.PublishAsync(new AnnouncementPublished(announcement.OrganizationId,
                    announcement.Id, announcement.LearningClassId, now));
            }
            catch (Exception exception)
            {
                loggerFactory.CreateLogger("LearningPlatform.Communication").LogWarning(exception,
                    "Unable to enqueue announcement notifications for {AnnouncementId}.",
                    announcement.Id);
            }
        }

        return Results.Ok(AnnouncementResponse.From(announcement));
    }


    internal static Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken) =>
        actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal)
            ? Task.FromResult(true)
            : CanManageClassAsCreatorOrTeacherAsync(actor, classId, academics, cancellationToken);


    internal static async Task<bool> CanManageClassAsCreatorOrTeacherAsync(AuthenticatedActor actor,
        Guid classId, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
            cancellationToken) || await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId,
            actor.UserId, cancellationToken);

}
