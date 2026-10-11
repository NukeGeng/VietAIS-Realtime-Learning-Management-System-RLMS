using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Api.GradingEndpoints;

namespace LearningPlatform.Assessment.Features.Grading.GetStudentResult;

internal static class GetStudentResultEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}/result", GetStudentResultAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
    }


    private static async Task<IResult> GetStudentResultAsync(Guid activityId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null || !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                activity.LearningClassId, actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!AssessmentRules.IsResultsReleased(activity, DateTimeOffset.UtcNow))
        {
            // Keep unreleased exam results indistinguishable from a missing result.
            return Results.NotFound();
        }

        var entry = (await session.Query<GradeEntry>()
                .Where(value => value.ActivityId == activityId && value.StudentId == actor.UserId &&
                    value.Status == GradeEntryStatus.Published)
                .OrderByDescending(value => value.AttemptNumber)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return entry is null || entry.Status != GradeEntryStatus.Published
            ? Results.NotFound()
            : Results.Ok(StudentResultResponse.From(entry));
    }

}
