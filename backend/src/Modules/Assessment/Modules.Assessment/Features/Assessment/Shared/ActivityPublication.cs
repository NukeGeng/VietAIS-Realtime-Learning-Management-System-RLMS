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

internal static class ActivityPublication
{

    internal static async Task<IResult> PublishActivityCoreAsync(
        Guid activityId,
        LiveSession? liveSession,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (liveSession is not null && liveSession.LearningClassId != activity.LearningClassId)
        {
            return Results.BadRequest(new { error = "The activity and live session belong to different classes." });
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId,
            activity.LearningClassId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot publish an activity." });
        }

        if (activity.Status == AssessmentActivityStatus.Published)
        {
            if (liveSession is not null && activity.LiveSessionId != liveSession.Id)
            {
                return Results.Conflict(new { error = "A published activity cannot move to another live session." });
            }

            return Results.Ok(AssessmentActivityResponse.From(activity));
        }

        if (activity.Status is AssessmentActivityStatus.Paused or AssessmentActivityStatus.Closed)
        {
            return Results.Conflict(new { error = "This activity cannot be published in its current state." });
        }

        if (activity.Questions.Count == 0)
        {
            return Results.Conflict(new { error = "An activity must contain at least one question." });
        }

        activity.Status = AssessmentActivityStatus.Published;
        activity.LiveSessionId = liveSession?.Id;
        activity.PublishedAt = DateTimeOffset.UtcNow;
        activity.UpdatedAt = activity.PublishedAt.Value;
        await GradingWorkflow.EnsureGradeItemAsync(session, activity, activity.PublishedAt.Value,
            cancellationToken);
        session.Update(activity);
        await session.SaveChangesAsync(cancellationToken);
        await PublishActivityReportingRefreshAsync(messageBus, loggerFactory, activity);
        await PublishActivityNotificationAsync(messageBus, loggerFactory, activity);
        return Results.Ok(AssessmentActivityResponse.From(activity));
    }



    internal static async Task<IResult> ChangeActivityStatusAsync(
        Guid activityId,
        AssessmentActivityStatus target,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        IMessageBus messageBus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId,
            activity.LearningClassId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot change an activity." });
        }

        var valid = (target, activity.Status) switch
        {
            (AssessmentActivityStatus.Paused, AssessmentActivityStatus.Published) => true,
            (AssessmentActivityStatus.Published, AssessmentActivityStatus.Paused) => true,
            (AssessmentActivityStatus.Closed, AssessmentActivityStatus.Published or AssessmentActivityStatus.Paused) => true,
            _ => false
        };
        if (!valid)
        {
            return Results.Conflict(new { error = "The activity cannot make that state transition." });
        }

        activity.Status = target;
        activity.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(activity);
        await session.SaveChangesAsync(cancellationToken);
        await PublishActivityReportingRefreshAsync(messageBus, loggerFactory, activity);
        return Results.Ok(AssessmentActivityResponse.From(activity));
    }

}
