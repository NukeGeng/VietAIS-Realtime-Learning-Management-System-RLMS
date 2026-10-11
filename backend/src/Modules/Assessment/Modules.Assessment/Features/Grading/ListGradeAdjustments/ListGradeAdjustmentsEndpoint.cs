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

namespace LearningPlatform.Assessment.Features.Grading.ListGradeAdjustments;

internal static class ListGradeAdjustmentsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/grade-entries/{gradeEntryId:guid}/adjustments",
                ListGradeAdjustmentsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
    }


    private static async Task<IResult> ListGradeAdjustmentsAsync(Guid gradeEntryId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var entry = await session.LoadAsync<GradeEntry>(gradeEntryId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, entry.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var adjustments = await session.Query<GradeAdjustment>()
            .Where(adjustment => adjustment.GradeEntryId == gradeEntryId)
            .OrderByDescending(adjustment => adjustment.AdjustedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(adjustments.Select(GradeAdjustmentResponse.From));
    }

}
