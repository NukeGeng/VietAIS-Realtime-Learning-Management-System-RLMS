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

namespace LearningPlatform.Assessment.Features.Assessment.CreateQuestionBankItem;

internal static class CreateQuestionBankItemEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/question-bank", CreateQuestionBankItemAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> CreateQuestionBankItemAsync(
        CreateQuestionBankItemRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var topic = AssessmentRules.RequireTopic(request.Topic);
            var prompt = AssessmentRules.RequirePrompt(request.Prompt);
            var options = NormalizeOptions(request.Options);
            var correctAnswers = NormalizeAnswers(request.CorrectAnswers);
            AssessmentRules.ValidateQuestion(request.Type, options, correctAnswers);
            if (request.CourseId is { } courseId &&
                await session.LoadAsync<Course>(courseId, cancellationToken) is null)
            {
                return Results.BadRequest(new { error = "The selected course does not exist." });
            }

            var now = DateTimeOffset.UtcNow;
            var item = new QuestionBankItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                CourseId = request.CourseId,
                Topic = topic,
                Difficulty = AssessmentRules.NormalizeDifficulty(request.Difficulty),
                CurrentVersion = 1,
                Status = QuestionBankStatus.Active,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            var version = new QuestionVersion
            {
                Id = AssessmentConstants.QuestionVersionId(item.Id, 1),
                OrganizationId = actor.OrganizationId,
                QuestionBankItemId = item.Id,
                Version = 1,
                Prompt = prompt,
                Type = request.Type,
                Options = options,
                CorrectAnswers = correctAnswers,
                CreatedAt = now,
                CreatedBy = actor.UserId
            };
            session.Insert(item);
            session.Insert(version);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/question-bank/{item.Id:D}",
                QuestionBankItemDetailResponse.From(item, [version]));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
