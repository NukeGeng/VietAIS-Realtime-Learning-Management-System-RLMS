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

namespace LearningPlatform.Assessment.Features.Assessment.AddActivityQuestion;

internal static class AddActivityQuestionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/questions",
                AddActivityQuestionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> AddActivityQuestionAsync(
        Guid activityId,
        AddActivityQuestionRequest request,
        HttpContext context,
        IDocumentSession session,
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

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (activity.Status != AssessmentActivityStatus.Draft)
        {
            return Results.Conflict(new { error = "Questions cannot be changed after an activity is published." });
        }

        if (activity.Questions.Count >= AssessmentRules.MaxQuestionsPerActivity)
        {
            return Results.BadRequest(new { error = "The activity has reached its question limit." });
        }

        var version = await session.LoadAsync<QuestionVersion>(request.QuestionVersionId, cancellationToken);
        if (version is null)
        {
            return Results.BadRequest(new { error = "The selected question version does not exist." });
        }

        var item = await session.LoadAsync<QuestionBankItem>(version.QuestionBankItemId, cancellationToken);
        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, activity.LearningClassId,
            cancellationToken);
        if (item is null || learningClass is null || item.CourseId is { } courseId && courseId != learningClass.CourseId)
        {
            return Results.BadRequest(new { error = "The question does not belong to this class's course." });
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot change an activity." });
        }

        try
        {
            var order = request.Order < 0 ? throw new AssessmentValidationException("Question order cannot be negative.") : request.Order;
            var points = AssessmentRules.RequirePoints(request.Points);
            if (activity.Questions.Any(question => question.Order == order))
            {
                return Results.Conflict(new { error = "A question with that order already exists." });
            }

            if (activity.Questions.Any(question => question.QuestionVersionId == version.Id))
            {
                return Results.Conflict(new { error = "The question version is already part of this activity." });
            }

            var activityQuestion = new ActivityQuestion
            {
                Id = AssessmentConstants.ActivityQuestionId(activity.Id, version.Id),
                QuestionVersionId = version.Id,
                Order = order,
                Points = points,
                Prompt = version.Prompt,
                Type = version.Type,
                Options = version.Options,
                CorrectAnswers = version.CorrectAnswers
            };
            activity.Questions.Add(activityQuestion);
            activity.Questions = activity.Questions.OrderBy(question => question.Order).ToList();
            activity.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(activity);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(AssessmentActivityResponse.From(activity));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
