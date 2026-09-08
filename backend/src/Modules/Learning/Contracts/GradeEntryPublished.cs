namespace LearningPlatform.Learning.Contracts;

public sealed record GradeEntryPublished(Guid OrganizationId, Guid LearningClassId, Guid? CourseId,
    Guid StudentId, Guid ActivityId, Guid GradeItemId, Guid GradeEntryId, string ActivityTitle,
    int AttemptNumber, decimal Score, decimal MaxScore, int GradeVersion,
    DateTimeOffset PublishedAt, DateTimeOffset? ResultsReleaseAt = null);

/// <summary>
/// Describes the effective student-visible grade after a published entry is
/// adjusted. The effective entry can be absent when an adjustment moves the
/// last published grade back to draft.
/// </summary>
public sealed record GradeEntryVisibilityChanged(Guid OrganizationId, Guid LearningClassId,
    Guid? CourseId, Guid StudentId, Guid ActivityId, Guid GradeEntryId, string ActivityTitle,
    bool IsPublished, int AttemptNumber, decimal Score, decimal MaxScore, int GradeVersion,
    DateTimeOffset ChangedAt, DateTimeOffset? ResultsReleaseAt = null);

/// <summary>
/// Integration event for the asynchronous Reporting read model. It is separate
/// from the Learning progress event so each projection has an independent queue
/// and failure domain.
/// </summary>
public sealed record GradeEntryPublishedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid StudentId, Guid GradeEntryId, DateTimeOffset PublishedAt);

/// <summary>
/// Refreshes reporting after the effective published grade changes, including
/// when an adjustment withdraws the last visible grade.
/// </summary>
public sealed record GradeEntryVisibilityChangedForReporting(Guid OrganizationId,
    Guid LearningClassId, Guid StudentId);

/// <summary>
/// Refreshes class and enrolled-student reporting snapshots after a grade-item
/// configuration changes, such as its weight.
/// </summary>
public sealed record GradeItemChangedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid GradeItemId);

/// <summary>
/// Refreshes the reporting read models after a durable lesson progress change.
/// The write endpoint remains independent from reporting availability.
/// </summary>
public sealed record LessonProgressChangedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid StudentId);
