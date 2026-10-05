namespace LearningPlatform.Learning.Domain;

public enum LearningProgressStatus
{
    InProgress,
    Completed
}

public sealed class StudentProgressRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActivityId { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public string ActivityTitle { get; set; } = "";
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Completion { get; set; }
    public DateTimeOffset? ResultsReleaseAt { get; set; }
    public LearningProgressStatus Status { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}

public sealed class LearningHistoryEntry
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid StudentId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid GradeEntryId { get; set; }
    public int GradeVersion { get; set; }
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public enum LessonProgressStatus
{
    InProgress,
    Completed
}

public sealed class StudentLessonProgressRecord
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid CourseId { get; set; }
    /// <summary>
    /// Nullable for compatibility with lesson progress written before
    /// curriculum context was introduced.
    /// </summary>
    public Guid? CurriculumId { get; set; }
    public Guid LessonId { get; set; }
    public Guid StudentId { get; set; }
    public LessonProgressStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum LearningResourceType
{
    Document,
    Slide,
    Video,
    Attachment,
    Recording
}

public enum LearningResourceStatus
{
    Draft,
    Published,
    Archived
}

/// <summary>
/// Metadata for a lesson/class resource. Binary content stays in object storage;
/// this document only keeps the tenant-owned reference and display metadata.
/// </summary>
public sealed class LearningResource
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CourseId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? LiveSessionId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public LearningResourceType Type { get; set; }
    public string? ExternalUrl { get; set; }
    public string? StorageKey { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public int? DurationSeconds { get; set; }
    public LearningResourceStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
