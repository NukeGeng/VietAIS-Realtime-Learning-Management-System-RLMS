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

namespace LearningPlatform.Assessment.Features.Assessment.GetAttempt;

internal static class GetAttemptEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}/attempt",
                GetAttemptAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> GetAttemptAsync(
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

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var attempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        return attempt is null ? Results.NotFound() : Results.Ok(StudentActivityResponse.From(attempt, activity));
    }

}
