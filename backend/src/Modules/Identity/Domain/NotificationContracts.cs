namespace LearningPlatform.Identity.Domain;

/// <summary>
/// Cross-module request for an in-app notification. The communication module
/// owns delivery and persistence; producers only enqueue this best-effort event
/// after their business transaction commits.
/// </summary>
public sealed record NotificationRequested(Guid EventId, Guid OrganizationId, Guid UserId,
    string Type, string Title, string Body, Guid? SourceId, DateTimeOffset OccurredAt);

/// <summary>
/// Cross-module request for a class-wide in-app notification. Communication owns
/// the enrollment fan-out so the producer does not load every student or write
/// notification documents on its critical path.
/// </summary>
public sealed record ClassNotificationRequested(Guid EventId, Guid OrganizationId,
    Guid LearningClassId, string Type, string Title, string Body, Guid? SourceId,
    DateTimeOffset OccurredAt);
