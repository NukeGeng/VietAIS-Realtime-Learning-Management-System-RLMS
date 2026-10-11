using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Features.Assessment.Shared.ActivityPublication;
using static LearningPlatform.Assessment.Features.Assessment.Shared.ActivityNotifications;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptTransitions;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AssessmentAccess;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptAvailability;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptSnapshots;
using static LearningPlatform.Assessment.Features.Assessment.Shared.QuestionValidation;

namespace LearningPlatform.Assessment.Features.Assessment.Shared;

internal static class ActivityNotifications
{


    internal static async Task PublishActivityReportingRefreshAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, AssessmentActivity activity)
    {
        try
        {
            await messageBus.PublishAsync(new AssessmentActivityChangedForReporting(
                activity.OrganizationId, activity.LearningClassId, activity.Id));
        }
        catch (Exception exception)
        {
            // The activity lifecycle change is durable even if the asynchronous
            // reporting queue is temporarily unavailable.
            loggerFactory.CreateLogger("LearningPlatform.Assessment.Activity").LogWarning(exception,
                "Unable to enqueue activity reporting refresh for {ActivityId}.", activity.Id);
        }
    }



    internal static async Task PublishActivityNotificationAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, AssessmentActivity activity)
    {
        try
        {
            var due = activity.DueAt is { } dueAt
                ? $" Hạn nộp {dueAt:dd/MM/yyyy HH:mm}."
                : "";
            await messageBus.PublishAsync(new ClassNotificationRequested(
                NotificationEventIds.Transition(activity.Id, "activity-published",
                    activity.PublishedAt ?? DateTimeOffset.UtcNow),
                activity.OrganizationId, activity.LearningClassId, "activity-published",
                $"Hoạt động mới: {activity.Title}",
                $"Một {ActivityKindLabel(activity.Kind)} mới đã được phát hành.{due}",
                activity.Id, activity.PublishedAt ?? DateTimeOffset.UtcNow));

            if (activity.DueAt is { } deadline && deadline > DateTimeOffset.UtcNow)
            {
                var reminderAt = deadline - TimeSpan.FromHours(24);
                if (reminderAt < DateTimeOffset.UtcNow)
                {
                    reminderAt = DateTimeOffset.UtcNow;
                }

                await messageBus.ScheduleAsync(new ClassNotificationRequested(
                    NotificationEventIds.Transition(activity.Id, "activity-deadline-reminder",
                        reminderAt),
                    activity.OrganizationId, activity.LearningClassId, "activity-deadline-reminder",
                    $"Sắp đến hạn: {activity.Title}",
                    $"Hoạt động đến hạn lúc {deadline:dd/MM/yyyy HH:mm}.", activity.Id, reminderAt), reminderAt);
            }
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LearningPlatform.Assessment.Activity").LogWarning(exception,
                "Unable to enqueue activity notification for {ActivityId}.", activity.Id);
        }
    }



    internal static string ActivityKindLabel(AssessmentActivityKind kind) => kind switch
    {
        AssessmentActivityKind.Exam => "bài thi",
        AssessmentActivityKind.Quiz => "bài trắc nghiệm",
        AssessmentActivityKind.Exercise => "bài luyện tập",
        _ => "bài tập"
    };



    internal static async Task PublishSubmissionReportingRefreshAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, Guid organizationId, Guid classId, Guid studentId)
    {
        try
        {
            await messageBus.PublishAsync(new SubmissionChangedForReporting(organizationId, classId,
                studentId));
        }
        catch (Exception exception)
        {
            // Submission and grade drafts are durable even if the asynchronous
            // reporting queue is temporarily unavailable.
            loggerFactory.CreateLogger("LearningPlatform.Assessment.Submission").LogWarning(exception,
                "Unable to enqueue submission reporting refresh for {ClassId}/{StudentId}.",
                classId, studentId);
        }
    }

}
