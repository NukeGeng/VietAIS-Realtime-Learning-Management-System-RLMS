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

namespace LearningPlatform.Assessment.Features.Assessment.CreateQuestionVersion;

internal static class CreateQuestionVersionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/question-bank/{itemId:guid}/versions",
                CreateQuestionVersionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> CreateQuestionVersionAsync(
        Guid itemId,
        CreateQuestionVersionRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var item = await session.LoadAsync<QuestionBankItem>(itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (item.Status == QuestionBankStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived question cannot receive a new version." });
        }

        try
        {
            var options = NormalizeOptions(request.Options);
            var correctAnswers = NormalizeAnswers(request.CorrectAnswers);
            AssessmentRules.ValidateQuestion(request.Type, options, correctAnswers);
            var versionNumber = item.CurrentVersion + 1;
            var now = DateTimeOffset.UtcNow;
            var version = new QuestionVersion
            {
                Id = AssessmentConstants.QuestionVersionId(item.Id, versionNumber),
                OrganizationId = actor!.OrganizationId,
                QuestionBankItemId = item.Id,
                Version = versionNumber,
                Prompt = AssessmentRules.RequirePrompt(request.Prompt),
                Type = request.Type,
                Options = options,
                CorrectAnswers = correctAnswers,
                CreatedAt = now,
                CreatedBy = actor.UserId
            };
            item.CurrentVersion = versionNumber;
            item.UpdatedAt = now;
            session.Insert(version);
            session.Update(item);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/question-bank/{item.Id:D}/versions/{version.Version}",
                QuestionVersionResponse.From(version));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
