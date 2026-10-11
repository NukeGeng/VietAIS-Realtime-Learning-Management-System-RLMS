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

namespace LearningPlatform.Assessment.Features.Grading.GetGradeEntry;

internal static class GetGradeEntryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/submissions/{submissionId:guid}/grade", GetGradeEntryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
    }


    private static async Task<IResult> GetGradeEntryAsync(Guid submissionId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var submission = await session.LoadAsync<Submission>(submissionId, cancellationToken);
        if (submission is null)
        {
            return Results.NotFound();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(submission.ActivityId, cancellationToken);
        if (activity is null || !await CanManageClassAsync(actor!, activity.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var entry = await session.LoadAsync<GradeEntry>(
            AssessmentConstants.GradeEntryId(activity.Id, submission.StudentId, submission.AttemptNumber),
            cancellationToken);
        return entry is null ? Results.NotFound() : Results.Ok(GradeEntryResponse.From(entry));
    }

}
