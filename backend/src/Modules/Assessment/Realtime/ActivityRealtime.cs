using LearningPlatform.Classroom.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wolverine;

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
    DateTimeOffset StartedAt, DateTimeOffset LastActivityAt, DateTimeOffset? SubmittedAt, long Version)
{
    public static StudentActivityRealtimeState From(StudentActivity activity) => new(activity.Id,
        activity.OrganizationId, activity.StudentId, activity.ActivityId, activity.LearningClassId,
        activity.LiveSessionId, activity.CurrentQuestionId, activity.Status,
        new Dictionary<Guid, string>(activity.Answers), activity.AnsweredCount, activity.StartedAt,
        activity.LastActivityAt, activity.SubmittedAt, activity.Version);
}

public sealed record ActivityOverviewDelta(Guid StudentId, Guid AttemptId, Guid? CurrentQuestionId,
    int AnsweredCount, int TotalQuestions, decimal Progress, StudentActivityStatus Status,
    DateTimeOffset LastActivityAt, long Version);

public sealed record ActivityDetailDelta(Guid AttemptId, long Version, string ChangeType,
    Guid? QuestionId, string? AnswerValue, Guid? CurrentQuestionId, int AnsweredCount,
    StudentActivityStatus Status, DateTimeOffset LastActivityAt);

public interface IActivityRealtimeNotifier
{
    Task NotifyAsync(Guid organizationId, Guid learningClassId, Guid activityId, Guid attemptId,
        ActivityOverviewDelta overview, ActivityDetailDelta detail, CancellationToken cancellationToken = default);
}

public sealed class ActivityRealtimeNotifier(
    IHubContext<AssessmentRealtimeHub> hub,
    ILogger<ActivityRealtimeNotifier> logger) : IActivityRealtimeNotifier
{
    public async Task NotifyAsync(Guid organizationId, Guid learningClassId, Guid activityId,
        Guid attemptId, ActivityOverviewDelta overview, ActivityDetailDelta detail,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(ActivityRealtimeGroups.Overview(organizationId, learningClassId, activityId))
                .SendAsync("activity-overview-delta", overview, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Activity overview realtime delivery failed for attempt {AttemptId}.",
                attemptId);
        }

        try
        {
            await hub.Clients.Group(ActivityRealtimeGroups.Detail(organizationId, attemptId))
                .SendAsync("activity-detail-delta", detail, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Activity detail realtime delivery failed for attempt {AttemptId}.",
                attemptId);
        }
    }
}

public static class StudentActivityChangePublisher
{
    public static async Task PublishAsync(IMessageBus messageBus,
        ILogger logger, StudentActivityChanged message)
    {
        try
        {
            await messageBus.PublishAsync(message);
        }
        catch (Exception exception)
        {
            // A realtime/projection queue outage must not roll back a committed answer.
            logger.LogWarning(exception, "StudentActivity change delivery was not queued for {AttemptId}.",
                message.AttemptId);
        }
    }
}
