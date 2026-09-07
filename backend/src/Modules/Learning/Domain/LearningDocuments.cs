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
