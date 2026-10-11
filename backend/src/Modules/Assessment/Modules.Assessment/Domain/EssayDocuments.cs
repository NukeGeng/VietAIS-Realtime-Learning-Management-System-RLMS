namespace LearningPlatform.Assessment.Domain;

public enum EssayAssignmentStatus
{
    Draft,
    Published,
    Closed
}

public enum EssayGroupStatus
{
    Active,
    Archived
}

public enum EssayTopicStatus
{
    Draft,
    PendingReview,
    ChangesRequested,
    Approved,
    Rejected,
    Locked
}

public enum EssaySubmissionStatus
{
    Rejected,
    Submitted,
    Superseded,
    Graded
}

public enum EssayTopicChangeRequestStatus
{
    PendingReview,
    Approved,
    Rejected
}

public sealed class EssayAssignment
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid LearningClassId { get; set; }
    public Guid? CourseId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset? Deadline { get; set; }
    public int MaxScore { get; set; } = 100;
    public int MaxGroupSize { get; set; } = 1;
    public bool AllowResubmission { get; set; }
    public EssayAssignmentStatus Status { get; set; }
    public ReportValidationPolicy ValidationPolicy { get; set; } = new();
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class EssayGroup
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EssayAssignmentId { get; set; }
    public Guid LearningClassId { get; set; }
    public string Name { get; set; } = "";
    public EssayGroupStatus Status { get; set; }
    public List<EssayGroupMember> Members { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record EssayGroupMember(Guid StudentId, DateTimeOffset JoinedAt);

public sealed class EssayTopic
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EssayAssignmentId { get; set; }
    public Guid EssayGroupId { get; set; }
    public string ProposedTitle { get; set; } = "";
    public string? CanonicalTitle { get; set; }
    public EssayTopicStatus Status { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class EssayTopicChangeRequest
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EssayAssignmentId { get; set; }
    public Guid EssayGroupId { get; set; }
    public Guid EssayTopicId { get; set; }
    public string PreviousCanonicalTitle { get; set; } = "";
    public EssayTopicStatus PreviousStatus { get; set; }
    public string ProposedTitle { get; set; } = "";
    public EssayTopicChangeRequestStatus Status { get; set; }
    public Guid RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class ReportValidationPolicy
{
    public string[] AllowedFileTypes { get; set; } = ["application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];
    public long MaxFileBytes { get; set; } = 25 * 1024 * 1024;
    public int? MinimumPages { get; set; }
    public int? MinimumWords { get; set; }
    public int? MinimumCharacters { get; set; }
    public string[] RequiredSections { get; set; } = [];
    public bool RequireTopicMatch { get; set; } = true;
}

public sealed class FileAsset
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string StorageKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class EssaySubmission
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EssayAssignmentId { get; set; }
    public Guid EssayGroupId { get; set; }
    public Guid FileAssetId { get; set; }
    public int Version { get; set; }
    public EssaySubmissionStatus Status { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ValidationResultId { get; set; }
}

public sealed class ReportValidationResult
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid EssaySubmissionId { get; set; }
    public bool IsValid { get; set; }
    public string? ExtractedTitle { get; set; }
    public string? NormalizedTitle { get; set; }
    public int? PageCount { get; set; }
    public int? WordCount { get; set; }
    public int? CharacterCount { get; set; }
    public string[] MissingSections { get; set; } = [];
    public string[] Errors { get; set; } = [];
    public DateTimeOffset ValidatedAt { get; set; }
}
