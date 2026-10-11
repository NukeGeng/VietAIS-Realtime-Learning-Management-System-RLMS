using LearningPlatform.Classroom.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Assessment.Realtime;

public interface IActivityRealtimeNotifier
{
    Task NotifyAsync(Guid organizationId, Guid learningClassId, Guid activityId, Guid attemptId,
        ActivityOverviewDelta? overview, ActivityDetailDelta? detail,
        CancellationToken cancellationToken = default);
}

public sealed class ActivityRealtimeNotifier(
    IHubContext<AssessmentRealtimeHub> hub,
    ILogger<ActivityRealtimeNotifier> logger) : IActivityRealtimeNotifier
{
    public async Task NotifyAsync(Guid organizationId, Guid learningClassId, Guid activityId,
        Guid attemptId, ActivityOverviewDelta? overview, ActivityDetailDelta? detail,
        CancellationToken cancellationToken = default)
    {
        if (overview is not null)
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
        }

        if (detail is not null)
        {
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
