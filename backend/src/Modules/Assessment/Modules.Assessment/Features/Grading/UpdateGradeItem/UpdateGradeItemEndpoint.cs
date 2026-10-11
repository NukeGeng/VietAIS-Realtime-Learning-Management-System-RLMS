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

namespace LearningPlatform.Assessment.Features.Grading.UpdateGradeItem;

internal static class UpdateGradeItemEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/assessment/grade-items/{gradeItemId:guid}", UpdateGradeItemAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
    }


    private static async Task<IResult> UpdateGradeItemAsync(Guid gradeItemId,
        UpdateGradeItemRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var item = await session.LoadAsync<GradeItem>(gradeItemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, item.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (item.Status == GradeItemStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived grade item cannot be changed." });
        }

        try
        {
            var previousWeight = item.Weight;
            item.Weight = GradingRules.RequireWeight(request.Weight);
            item.UpdatedAt = DateTimeOffset.UtcNow;
            session.Store(item);
            await session.SaveChangesAsync(cancellationToken);
            if (item.Weight != previousWeight)
            {
                try
                {
                    await messageBus.PublishAsync(new GradeItemChangedForReporting(
                        actor!.OrganizationId, item.LearningClassId, item.Id));
                }
                catch (Exception exception)
                {
                    // Reporting is asynchronous and must not make a durable
                    // grade-item configuration change fail.
                    loggerFactory.CreateLogger("LearningPlatform.Assessment.Grading").LogWarning(exception,
                        "Unable to enqueue grade-item reporting refresh for {GradeItemId}.",
                        item.Id);
                }
            }
            return Results.Ok(GradebookItemResponse.From(item));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
