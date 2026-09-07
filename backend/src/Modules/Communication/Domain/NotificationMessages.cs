using LearningPlatform.Identity.Domain;
using LearningPlatform.Academics.Domain;
using Marten;

namespace LearningPlatform.Communication.Domain;

public sealed record AnnouncementPublished(Guid OrganizationId, Guid AnnouncementId,
    Guid LearningClassId, DateTimeOffset PublishedAt);

public static class NotificationRequestedHandler
{
    public static async Task Handle(NotificationRequested message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        if (!IsValid(message.OrganizationId, message.UserId, message.Type, message.Title,
                message.Body))
        {
            return;
        }

        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var notificationId = CommunicationConstants.NotificationId(message.UserId, message.Type,
            message.SourceId, message.EventId);
        var notification = await session.LoadAsync<Notification>(notificationId, cancellationToken);
        var isNew = notification is null;
        notification ??= new Notification
            {
                Id = notificationId,
                OrganizationId = message.OrganizationId,
                UserId = message.UserId,
                Type = message.Type.Trim(),
                Title = message.Title.Trim(),
                Body = message.Body.Trim(),
                SourceId = message.SourceId,
                CreatedAt = message.OccurredAt
            };
        if (isNew)
        {
            session.Insert(notification);
        }
        else
        {
            session.Store(notification);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static bool IsValid(Guid organizationId, Guid userId, string type, string title,
        string body) => organizationId != Guid.Empty && userId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(type) && type.Trim().Length <= CommunicationConstants.MaxNotificationTypeLength &&
        !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= CommunicationConstants.MaxNotificationTitleLength &&
        !string.IsNullOrWhiteSpace(body) && body.Trim().Length <= CommunicationConstants.MaxNotificationBodyLength;
}

public static class ClassNotificationRequestedHandler
{
    public static async Task Handle(ClassNotificationRequested message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        if (message.OrganizationId == Guid.Empty || message.LearningClassId == Guid.Empty ||
            string.IsNullOrWhiteSpace(message.Type) ||
            message.Type.Trim().Length > CommunicationConstants.MaxNotificationTypeLength ||
            string.IsNullOrWhiteSpace(message.Title) ||
            message.Title.Trim().Length > CommunicationConstants.MaxNotificationTitleLength ||
            string.IsNullOrWhiteSpace(message.Body) ||
            message.Body.Trim().Length > CommunicationConstants.MaxNotificationBodyLength)
        {
            return;
        }

        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var studentIds = await session.Query<Enrollment>()
            .Where(enrollment => enrollment.LearningClassId == message.LearningClassId &&
                enrollment.Status == EnrollmentStatus.Active)
            .Select(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);
        foreach (var studentId in studentIds)
        {
            var notificationId = CommunicationConstants.NotificationId(studentId,
                message.Type.Trim(), message.SourceId, message.EventId);
            if (await session.LoadAsync<Notification>(notificationId, cancellationToken) is not null)
            {
                continue;
            }

            session.Insert(new Notification
            {
                Id = notificationId,
                OrganizationId = message.OrganizationId,
                UserId = studentId,
                Type = message.Type.Trim(),
                Title = message.Title.Trim(),
                Body = message.Body.Trim(),
                SourceId = message.SourceId,
                CreatedAt = message.OccurredAt
            });
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}

public static class AnnouncementPublishedHandler
{
    public static async Task Handle(AnnouncementPublished message, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession(message.OrganizationId.ToString("D"));
        var announcement = await session.LoadAsync<Announcement>(message.AnnouncementId,
            cancellationToken);
        if (announcement is null || announcement.OrganizationId != message.OrganizationId ||
            announcement.LearningClassId != message.LearningClassId ||
            announcement.Status != AnnouncementStatus.Published)
        {
            return;
        }

        var students = await session.Query<Enrollment>()
            .Where(enrollment => enrollment.LearningClassId == message.LearningClassId &&
                enrollment.Status == EnrollmentStatus.Active)
            .Select(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);
        foreach (var studentId in students)
        {
            var notificationId = CommunicationConstants.AnnouncementNotificationId(studentId,
                announcement.Id);
            if (await session.LoadAsync<Notification>(notificationId, cancellationToken) is not null)
            {
                continue;
            }

            session.Insert(new Notification
            {
                Id = notificationId,
                OrganizationId = message.OrganizationId,
                UserId = studentId,
                Type = "announcement",
                Title = announcement.Title,
                Body = announcement.Content,
                SourceId = announcement.Id,
                CreatedAt = message.PublishedAt
            });
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
