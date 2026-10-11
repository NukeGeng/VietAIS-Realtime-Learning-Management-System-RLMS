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

namespace LearningPlatform.Assessment.Features.Assessment.CreateActivity;

internal static class CreateActivityEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/classes/{classId:guid}/activities", CreateActivityAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> CreateActivityAsync(
        Guid classId,
        CreateActivityRequest request,
        HttpContext context,
        IDocumentSession session,
        IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot receive a new activity." });
        }

        try
        {
            var times = ValidateActivityTimes(request.OpensAt, request.DueAt, request.TimeLimit);
            var examPolicy = AssessmentRules.ValidateExamPolicy(request.Kind, request.ExamPolicy);
            var now = DateTimeOffset.UtcNow;
            var activity = new AssessmentActivity
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                Title = AssessmentRules.RequireTitle(request.Title),
                Description = AssessmentRules.RequireDescription(request.Description),
                Kind = AssessmentRules.RequireActivityKind(request.Kind),
                Status = AssessmentActivityStatus.Draft,
                OpensAt = times.OpensAt,
                DueAt = times.DueAt,
                TimeLimit = times.TimeLimit,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                ExamPolicy = examPolicy
            };
            session.Insert(activity);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/activities/{activity.Id:D}",
                AssessmentActivityResponse.From(activity));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
