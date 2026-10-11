namespace LearningPlatform.Assessment.Contracts;

/// <summary>
/// Refreshes class and active-student reporting after a published assessment
/// activity changes lifecycle state.
/// </summary>
public sealed record AssessmentActivityChangedForReporting(Guid OrganizationId,
    Guid LearningClassId, Guid ActivityId);

/// <summary>
/// Refreshes class and active-student reporting after a published essay
/// assignment changes lifecycle state.
/// </summary>
public sealed record EssayAssignmentChangedForReporting(Guid OrganizationId,
    Guid LearningClassId, Guid AssignmentId);

/// <summary>
/// Refreshes class and student reporting after a durable accepted submission.
/// Rejected essay validation attempts are deliberately excluded.
/// </summary>
public sealed record SubmissionChangedForReporting(Guid OrganizationId, Guid LearningClassId,
    Guid StudentId);
