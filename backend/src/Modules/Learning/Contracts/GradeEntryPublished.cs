namespace LearningPlatform.Learning.Contracts;

public sealed record GradeEntryPublished(Guid OrganizationId, Guid LearningClassId, Guid? CourseId,
    Guid StudentId, Guid ActivityId, Guid GradeItemId, Guid GradeEntryId, string ActivityTitle,
    int AttemptNumber, decimal Score, decimal MaxScore, int GradeVersion,
    DateTimeOffset PublishedAt, DateTimeOffset? ResultsReleaseAt = null);

/// <summary>
/// Integration event for the asynchronous Reporting read model. It is separate
/// from the Learning progress event so each projection has an independent queue
/// and failure domain.
/// </summary>
public sealed record GradeEntryPublishedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid StudentId, Guid GradeEntryId, DateTimeOffset PublishedAt);

/// <summary>
/// Refreshes the reporting read models after a durable lesson progress change.
/// The write endpoint remains independent from reporting availability.
/// </summary>
public sealed record LessonProgressChangedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid StudentId);
