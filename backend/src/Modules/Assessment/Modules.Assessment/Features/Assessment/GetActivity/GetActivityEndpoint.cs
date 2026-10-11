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

namespace LearningPlatform.Assessment.Features.Assessment.GetActivity;

internal static class GetActivityEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}", GetActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> GetActivityAsync(
        Guid activityId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
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

        if (!await CanAccessActivityAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        Guid[]? questionOrder = null;
        var canManage = await CanManageClassAsync(actor!, activity.LearningClassId, academics,
            cancellationToken);
        if (!canManage && activity.ExamPolicy?.ShuffleQuestions == true)
        {
            var latestAttempt = await FindLatestAttemptAsync(session, activity.Id, actor!.UserId,
                cancellationToken);
            questionOrder = latestAttempt?.QuestionOrder is { Length: > 0 }
                ? latestAttempt.QuestionOrder
                : AssessmentRules.QuestionOrder(activity.Questions, true,
                    AssessmentConstants.StudentActivityId(activity.Id, actor.UserId, 1));
        }

        return Results.Ok(AssessmentActivityResponse.From(activity, questionOrder));
    }

}
