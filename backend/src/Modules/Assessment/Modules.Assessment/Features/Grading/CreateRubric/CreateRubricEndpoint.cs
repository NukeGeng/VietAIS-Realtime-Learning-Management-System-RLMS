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

namespace LearningPlatform.Assessment.Features.Grading.CreateRubric;

internal static class CreateRubricEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/rubric", CreateRubricAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> CreateRubricAsync(Guid activityId,
        CreateRubricRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        var existingGrades = await session.Query<GradeEntry>()
            .Where(entry => entry.ActivityId == activityId && entry.Status == GradeEntryStatus.Published)
            .AnyAsync(cancellationToken);
        if (existingGrades)
        {
            return Results.Conflict(new { error = "A rubric cannot change after a result is published." });
        }

        try
        {
            var criteria = (request.Criteria ?? []).Select((criterion, index) => new RubricCriterion(
                    criterion.Id == Guid.Empty ? Guid.NewGuid() : criterion.Id,
                    criterion.Name?.Trim() ?? "", criterion.Description?.Trim() ?? "", criterion.MaxPoints,
                    criterion.Order < 0 ? index : criterion.Order))
                .ToArray();
            GradingRules.ValidateRubric(criteria);
            var now = DateTimeOffset.UtcNow;
            var rubric = await session.LoadAsync<AssessmentRubric>(AssessmentConstants.RubricId(activityId),
                cancellationToken);
            if (rubric is null)
            {
                rubric = new AssessmentRubric
                {
                    Id = AssessmentConstants.RubricId(activityId),
                    OrganizationId = actor!.OrganizationId,
                    ActivityId = activityId,
                    LearningClassId = activity.LearningClassId,
                    CreatedBy = actor.UserId,
                    CreatedAt = now
                };
                session.Insert(rubric);
                rubric.Criteria = criteria;
                rubric.UpdatedAt = now;
                await session.SaveChangesAsync(cancellationToken);
                return Results.Ok(RubricResponse.From(rubric));
            }

            rubric.Criteria = criteria;
            rubric.UpdatedAt = now;
            session.Store(rubric);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(RubricResponse.From(rubric));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
