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

namespace LearningPlatform.Assessment.Features.Assessment.OpenQuestion;

internal static class OpenQuestionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/attempt/questions/{questionId:guid}/open",
                OpenQuestionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> OpenQuestionAsync(
        Guid activityId,
        Guid questionId,
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

        if (activity.Questions.All(question => question.Id != questionId))
        {
            return Results.NotFound();
        }

        if (!IsOpen(activity, DateTimeOffset.UtcNow) || activity.Status != AssessmentActivityStatus.Published)
        {
            return Results.Conflict(new { error = "The activity is not currently open." });
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before opening a question." });
        }

        var attemptId = currentAttempt.Id;
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before opening a question." });
        }

        if (stream.Aggregate.Status != StudentActivityStatus.InProgress ||
            !IsAttemptOpen(activity, stream.Aggregate, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "This activity attempt is not accepting interaction." });
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var openedEvent = new QuestionOpened(attemptId, questionId, now);
        var nextState = Clone(stream.Aggregate);
        nextState.Apply(openedEvent);
        stream.AppendOne(openedEvent);
        await session.SaveChangesAsync(cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId, ActivityChangeTypes.QuestionOpened, questionId, null,
                StudentActivityRealtimeStateFactory.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(attempt!, activity));
    }

}
