namespace LearningPlatform.Communication.Domain;

public sealed record AnnouncementPublished(Guid OrganizationId, Guid AnnouncementId,
    Guid LearningClassId, DateTimeOffset PublishedAt);
