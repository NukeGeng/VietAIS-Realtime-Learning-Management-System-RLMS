using LearningPlatform.Classroom.Domain;

namespace LearningPlatform.Assessment.Realtime;

public static class ActivityRealtimeGroups
{
    public static string Overview(Guid organizationId, Guid learningClassId, Guid activityId) =>
        $"tenant:{organizationId:D}:activity-overview:{learningClassId:D}:{activityId:D}";

    public static string Detail(Guid organizationId, Guid attemptId) =>
        $"tenant:{organizationId:D}:activity-detail:{attemptId:D}";
}

public static class ActivityChangeTypes
{
    public const string Started = "started";
    public const string QuestionOpened = "question-opened";
    public const string AnswerSubmitted = "answer-submitted";
    public const string AnswerChanged = "answer-changed";
    public const string Submitted = "submitted";
    public const string Idle = "idle";
    public const string Resumed = "resumed";
    public const string HelpRequested = "help-requested";
    public const string HelpAcknowledged = "help-acknowledged";
    public const string HelpResolved = "help-resolved";
}

/// <summary>
/// Tenant-carrying message used to update read models and realtime deltas after a
/// durable StudentActivity event has committed.
/// </summary>
public sealed record StudentActivityChanged(Guid OrganizationId, Guid AttemptId, Guid ActivityId,
    Guid StudentId, Guid LearningClassId, string ChangeType, Guid? QuestionId, string? AnswerValue,
    StudentActivityRealtimeState State, DateTimeOffset OccurredAt);

public sealed record StudentActivityRealtimeState(Guid AttemptId, Guid OrganizationId, Guid StudentId,
    Guid ActivityId, Guid LearningClassId, Guid? LiveSessionId, Guid? CurrentQuestionId,
    StudentActivityStatus Status, Dictionary<Guid, string> Answers, int AnsweredCount,
    DateTimeOffset StartedAt, DateTimeOffset LastActivityAt, DateTimeOffset? SubmittedAt, long Version,
    Guid[] QuestionOrder,
    int AttemptNumber = 1, bool HelpRequested = false,
    DateTimeOffset? HelpRequestedAt = null, DateTimeOffset? HelpAcknowledgedAt = null)
;

public sealed record ActivityOverviewDelta(Guid StudentId, Guid AttemptId, Guid? CurrentQuestionId,
    int AnsweredCount, int TotalQuestions, decimal Progress, StudentActivityStatus Status,
    DateTimeOffset LastActivityAt, long Version, int AttemptNumber = 1,
    ActivityQuestionProgressDelta[]? QuestionProgress = null);

public sealed record ActivityQuestionProgressDelta(Guid QuestionId, int AnsweredStudentCount,
    int CurrentStudentCount);

public sealed record ActivityDetailDelta(Guid AttemptId, long Version, string ChangeType,
    Guid? QuestionId, string? AnswerValue, Guid? CurrentQuestionId, int AnsweredCount,
    StudentActivityStatus Status, DateTimeOffset LastActivityAt, bool HelpRequested = false);
