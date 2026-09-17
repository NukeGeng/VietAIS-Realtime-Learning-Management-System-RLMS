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
        endpoints.MapGet("/api/communication/classes/{classId:guid}/announcements",
                ListAnnouncementsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAnnouncements);
        endpoints.MapPost("/api/communication/classes/{classId:guid}/announcements",
                CreateAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAnnouncements);
        endpoints.MapGet("/api/communication/announcements/{announcementId:guid}",
                GetAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAnnouncements);
        endpoints.MapPost("/api/communication/announcements/{announcementId:guid}/publish",
                PublishAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAnnouncements);
        endpoints.MapPost("/api/communication/announcements/{announcementId:guid}/archive",
                ArchiveAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAnnouncements);
        endpoints.MapGet("/api/communication/notifications", ListNotificationsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadNotifications);
        endpoints.MapPost("/api/communication/notifications/{notificationId:guid}/read",
                MarkNotificationReadAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadNotifications);
    }

    private static async Task<IResult> ListAnnouncementsAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<Announcement>()
            .Where(announcement => announcement.OrganizationId == actor.OrganizationId &&
                announcement.LearningClassId == classId);
        if (!canManage)
        {
            query = query.Where(announcement => announcement.Status == AnnouncementStatus.Published);
        }

        var announcements = await query.OrderByDescending(announcement => announcement.PublishedAt)
            .ThenByDescending(announcement => announcement.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Results.Ok(announcements.Select(AnnouncementResponse.From));
    }

    private static async Task<IResult> CreateAnnouncementAsync(Guid classId,
        CreateAnnouncementRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot receive a new announcement." });
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var announcement = new Announcement
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                AuthorId = actor.UserId,
                Title = CommunicationRules.RequireAnnouncementTitle(request.Title),
                Content = CommunicationRules.RequireAnnouncementContent(request.Content),
                Status = AnnouncementStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(announcement);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/communication/announcements/{announcement.Id:D}",
                AnnouncementResponse.From(announcement));
        }
        catch (CommunicationValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetAnnouncementAsync(Guid announcementId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var announcement = await session.LoadAsync<Announcement>(announcementId, cancellationToken);
        if (announcement is null || announcement.OrganizationId != actor!.OrganizationId)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, announcement.LearningClassId, academics,
            cancellationToken);
        if (!canManage && (announcement.Status != AnnouncementStatus.Published ||
                !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                    announcement.LearningClassId, actor.UserId, cancellationToken)))
        {
            return Results.Forbid();
        }

        return Results.Ok(AnnouncementResponse.From(announcement));
    }

    private static Task<IResult> PublishAnnouncementAsync(Guid announcementId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeAnnouncementStatusAsync(announcementId, AnnouncementStatus.Published, context, session,
            academics, messageBus, loggerFactory, cancellationToken);

    private static Task<IResult> ArchiveAnnouncementAsync(Guid announcementId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeAnnouncementStatusAsync(announcementId, AnnouncementStatus.Archived, context, session,
            academics, messageBus, loggerFactory, cancellationToken);

    private static async Task<IResult> ChangeAnnouncementStatusAsync(Guid announcementId,
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

    private static Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken) =>
        actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal)
            ? Task.FromResult(true)
            : CanManageClassAsCreatorOrTeacherAsync(actor, classId, academics, cancellationToken);

    private static async Task<bool> CanManageClassAsCreatorOrTeacherAsync(AuthenticatedActor actor,
        Guid classId, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
            cancellationToken) || await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId,
            actor.UserId, cancellationToken);
}

public sealed record CreateAnnouncementRequest(string? Title, string? Content);

public sealed record AnnouncementResponse(Guid Id, Guid LearningClassId, Guid AuthorId, string Title,
    string Content, AnnouncementStatus Status, DateTimeOffset? PublishedAt, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AnnouncementResponse From(Announcement announcement) => new(announcement.Id,
        announcement.LearningClassId, announcement.AuthorId, announcement.Title, announcement.Content,
        announcement.Status, announcement.PublishedAt, announcement.CreatedAt, announcement.UpdatedAt);
}

public sealed record NotificationResponse(Guid Id, Guid UserId, string Type, string Title, string Body,
    Guid? SourceId, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt)
{
    public static NotificationResponse From(Notification notification) => new(notification.Id,
        notification.UserId, notification.Type, LocalizedTitle(notification), LocalizedBody(notification),
        notification.SourceId, notification.ReadAt, notification.CreatedAt);

    internal static string LocalizedTitleForGrouping(Notification item) => LocalizedTitle(item);
    internal static string LocalizedBodyForGrouping(Notification item) => LocalizedBody(item);

    private static bool IsSchedule(Notification item) => item.Type.StartsWith("schedule-", StringComparison.Ordinal);
    private static string LocalizedTitle(Notification item) => !IsSchedule(item) ? item.Title : item.Title switch
    {
        "New class session" => "Lịch học mới",
        "Class session changed" => "Lịch học đã thay đổi",
        "Class session cancelled" => "Lịch học đã hủy",
        _ => item.Title
    };
    private static string LocalizedBody(Notification item) => !IsSchedule(item) ? item.Body : item.Body
        .Replace("A new class session has been scheduled.", "Một buổi học mới đã được lên lịch.", StringComparison.Ordinal)
        .Replace("A class session has been rescheduled.", "Buổi học đã được đổi lịch.", StringComparison.Ordinal)
        .Replace("A class session has been cancelled.", "Buổi học đã được hủy.", StringComparison.Ordinal)
        .Replace("Session:", "Thời gian (UTC):", StringComparison.Ordinal);
}
