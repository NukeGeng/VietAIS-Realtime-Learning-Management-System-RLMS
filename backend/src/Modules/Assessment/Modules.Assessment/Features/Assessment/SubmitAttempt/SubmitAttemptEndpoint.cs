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

namespace LearningPlatform.Assessment.Features.Assessment.SubmitAttempt;

internal static class SubmitAttemptEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/attempt/submit",
                SubmitAttemptAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> SubmitAttemptAsync(
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

        if (activity.Status != AssessmentActivityStatus.Published || !IsOpen(activity, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "The activity is not currently accepting submissions." });
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before submitting it." });
        }

        var attemptId = currentAttempt.Id;
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before submitting it." });
        }

        if (stream.Aggregate.Status == StudentActivityStatus.Submitted)
        {
            var existing = await session.LoadAsync<Submission>(AssessmentConstants.SubmissionId(
                activityId, actor.UserId, stream.Aggregate.AttemptNumber), cancellationToken);
            return existing is null ? Results.Conflict(new { error = "The activity is already submitted." }) :
                Results.Ok(SubmissionResponse.From(existing));
        }

        if (stream.Aggregate.Status != StudentActivityStatus.InProgress ||
            !IsAttemptOpen(activity, stream.Aggregate, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "This activity attempt cannot be submitted." });
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var submittedEvent = new ActivitySubmitted(attemptId, now);
        var nextState = Clone(stream.Aggregate);
        nextState.Apply(submittedEvent);
        stream.AppendOne(submittedEvent);
        var submission = new Submission
        {
            Id = AssessmentConstants.SubmissionId(activityId, actor.UserId,
                stream.Aggregate.AttemptNumber),
            OrganizationId = actor.OrganizationId,
            ActivityId = activityId,
            StudentId = actor.UserId,
            StudentActivityId = attemptId,
            AttemptNumber = stream.Aggregate.AttemptNumber,
            SubmittedAt = now,
            Status = SubmissionStatus.Submitted,
            Version = checked((int)(stream.Aggregate.Version + 1)),
            Answers = stream.Aggregate.Answers
                .Select(answer => new SubmissionAnswer(answer.Key, answer.Value))
                .ToArray()
        };
        session.Insert(submission);
        var gradeItem = await GradingWorkflow.EnsureGradeItemAsync(session, activity, now,
            cancellationToken);
        var automaticGrade = GradingRules.CalculateAutomaticGrade(activity, submission);
        var gradeEntry = GradingWorkflow.CreateGradeEntry(activity, submission, gradeItem,
            automaticGrade, now);
        session.Insert(gradeEntry);
        await session.SaveChangesAsync(cancellationToken);
        await PublishSubmissionReportingRefreshAsync(messageBus, loggerFactory,
            actor.OrganizationId, activity.LearningClassId, actor.UserId);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId, ActivityChangeTypes.Submitted, null, null,
                StudentActivityRealtimeStateFactory.From(nextState), now));
        return Results.Created($"/api/assessment/activities/{activityId:D}/submissions/{submission.Id:D}",
            SubmissionResponse.From(submission));
    }

}
