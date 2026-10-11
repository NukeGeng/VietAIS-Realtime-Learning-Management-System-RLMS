namespace LearningPlatform.Assessment.Domain;

public enum GradeItemStatus
{
    Active,
    Archived
}

public enum GradeEntryStatus
{
    Draft,
    Published
}

public enum GradeSource
{
    Automatic,
    Manual
}

public sealed class GradeItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? EssayAssignmentId { get; set; }
    public Guid? CourseId { get; set; }
    public string Title { get; set; } = "";
    public decimal MaxScore { get; set; }
    public decimal Weight { get; set; } = 1m;
    public GradeItemStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class GradeEntry
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? EssayAssignmentId { get; set; }
    public Guid? EssayGroupId { get; set; }
    public Guid GradeItemId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? SourceSubmissionId { get; set; }
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public GradeEntryStatus Status { get; set; }
    public GradeSource Source { get; set; }
    public bool IsComplete { get; set; }
    public string? Feedback { get; set; }
    public GradeQuestionResult[] QuestionResults { get; set; } = [];
    public Guid? GradedBy { get; set; }
    public DateTimeOffset? GradedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public bool? Passed { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Immutable audit record for a post-grading score adjustment. The grade entry
/// itself is moved back to draft and must be published again before students see
/// the adjusted result.
/// </summary>
public sealed class GradeAdjustment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid GradeEntryId { get; set; }
    public Guid GradeItemId { get; set; }
    public Guid StudentId { get; set; }
    public decimal PreviousScore { get; set; }
    public decimal NewScore { get; set; }
    public decimal MaxScore { get; set; }
    public string? PreviousFeedback { get; set; }
    public string? NewFeedback { get; set; }
    public string Reason { get; set; } = "";
    public Guid AdjustedBy { get; set; }
    public DateTimeOffset AdjustedAt { get; set; }
    public int GradeVersion { get; set; }
}

public sealed record GradeQuestionResult(Guid QuestionId, decimal Score, decimal MaxScore,
    string? Feedback, Guid? RubricCriterionId);

public sealed class AssessmentRubric
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid LearningClassId { get; set; }
    public RubricCriterion[] Criteria { get; set; } = [];
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record RubricCriterion(Guid Id, string Name, string Description, decimal MaxPoints,
    int Order);
