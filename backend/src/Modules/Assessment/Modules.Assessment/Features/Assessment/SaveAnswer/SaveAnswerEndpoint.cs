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

namespace LearningPlatform.Assessment.Features.Assessment.SaveAnswer;

internal static class SaveAnswerEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/assessment/activities/{activityId:guid}/attempt/answers/{questionId:guid}",
                SaveAnswerAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> SaveAnswerAsync(
        Guid activityId,
        Guid questionId,
        SaveAnswerRequest request,
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
            return Results.Conflict(new { error = "The activity is not currently accepting answers." });
        }

        var question = activity.Questions.SingleOrDefault(candidate => candidate.Id == questionId);
        if (question is null)
        {
            return Results.NotFound();
        }

        string answer;
        try
        {
            answer = NormalizeAnswer(question, AssessmentRules.RequireAnswer(request.Value));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before saving an answer." });
        }

        var attemptId = currentAttempt.Id;
        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId, cancellationToken);
        if (stream.Aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before saving an answer." });
        }

        if (stream.Aggregate.Status != StudentActivityStatus.InProgress ||
            !IsAttemptOpen(activity, stream.Aggregate, DateTimeOffset.UtcNow))
        {
            return Results.Conflict(new { error = "This activity attempt is not accepting answers." });
        }

        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        var now = DateTimeOffset.UtcNow;
        var changed = stream.Aggregate.Answers.ContainsKey(questionId);
        var answerEvent = changed
            ? (object)new AnswerChanged(attemptId, questionId, answer, now)
            : new AnswerSubmitted(attemptId, questionId, answer, now);
        var nextState = Clone(stream.Aggregate);
        if (answerEvent is AnswerChanged changedEvent)
        {
            nextState.Apply(changedEvent);
        }
        else
        {
            nextState.Apply((AnswerSubmitted)answerEvent);
        }
        stream.AppendOne(answerEvent);
        await session.SaveChangesAsync(cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, attemptId, activity.Id, actor.UserId,
                activity.LearningClassId,
                changed ? ActivityChangeTypes.AnswerChanged : ActivityChangeTypes.AnswerSubmitted,
                questionId, answer, StudentActivityRealtimeStateFactory.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(attempt!, activity));
    }

}
