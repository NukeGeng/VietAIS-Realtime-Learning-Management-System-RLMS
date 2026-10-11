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

namespace LearningPlatform.Assessment.Features.Assessment.StartAttempt;

internal static class StartAttemptEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {

        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/attempt/start",
                StartAttemptAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> StartAttemptAsync(
        Guid activityId,
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

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!IsOpen(activity, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "The activity is not currently open." });
        }

        var latestAttempt = await FindLatestAttemptAsync(session, activity.Id, actor!.UserId,
            cancellationToken);
        var attemptNumber = latestAttempt?.AttemptNumber ?? 0;
        if (latestAttempt is not null && latestAttempt.Status is
            (StudentActivityStatus.InProgress or StudentActivityStatus.Idle or StudentActivityStatus.Paused))
        {
            return Results.Ok(StudentActivityResponse.From(latestAttempt, activity));
        }

        if (latestAttempt is not null && (!IsExamRetakeAllowed(activity, attemptNumber)))
        {
            return Results.Conflict(new { error = "This activity attempt is already finished." });
        }

        attemptNumber = Math.Max(attemptNumber + 1, 1);
        var attemptId = AssessmentConstants.StudentActivityId(activity.Id, actor!.UserId,
            attemptNumber);
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is not null)
        {
            return Results.Ok(StudentActivityResponse.From(stream.Aggregate, activity));
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var questionOrder = AssessmentRules.QuestionOrder(activity.Questions,
            activity.ExamPolicy?.ShuffleQuestions == true, attemptId);
        var startedEvent = new StudentActivityStarted(attemptId, actor.OrganizationId, actor.UserId,
            activity.Id, activity.LearningClassId, activity.LiveSessionId, now, attemptNumber,
            questionOrder);
        var nextState = new StudentActivity();
        nextState.Apply(startedEvent);
        stream.AppendOne(startedEvent);
        await session.SaveChangesAsync(cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId, ActivityChangeTypes.Started, null, null,
                StudentActivityRealtimeStateFactory.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(attempt!, activity));
    }

}
