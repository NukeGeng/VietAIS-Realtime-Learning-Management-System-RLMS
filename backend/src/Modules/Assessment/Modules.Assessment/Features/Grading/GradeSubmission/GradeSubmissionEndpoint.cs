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

namespace LearningPlatform.Assessment.Features.Grading.GradeSubmission;

internal static class GradeSubmissionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/submissions/{submissionId:guid}/grade", GradeSubmissionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
    }


    private static async Task<IResult> GradeSubmissionAsync(Guid submissionId,
        GradeSubmissionRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        try
        {
            var item = await GradingWorkflow.EnsureGradeItemAsync(session, activity,
                DateTimeOffset.UtcNow, cancellationToken);
            var entry = await session.LoadAsync<GradeEntry>(
                AssessmentConstants.GradeEntryId(activity.Id, submission.StudentId, submission.AttemptNumber),
                cancellationToken);
            if (entry is null)
            {
                var automatic = GradingRules.CalculateAutomaticGrade(activity, submission);
                entry = GradingWorkflow.CreateGradeEntry(activity, submission, item, automatic,
                    DateTimeOffset.UtcNow);
                session.Insert(entry);
            }

            var rubric = request.RubricId is { } rubricId
                ? await session.LoadAsync<AssessmentRubric>(rubricId, cancellationToken)
                : null;
            if (request.RubricId is not null && (rubric is null || rubric.ActivityId != activity.Id))
            {
                return Results.BadRequest(new { error = "The selected rubric does not belong to this activity." });
            }

            var supplied = (request.Questions ?? []).ToArray();
            if (supplied.Select(value => value.QuestionId).Distinct().Count() != supplied.Length)
            {
                return Results.BadRequest(new { error = "A question can only be graded once." });
            }

            var questionById = activity.Questions.ToDictionary(question => question.Id);
            var suppliedById = supplied.ToDictionary(value => value.QuestionId);
            foreach (var value in supplied)
            {
                if (!questionById.TryGetValue(value.QuestionId, out var question))
                {
                    return Results.BadRequest(new { error = "The grade contains an unknown question." });
                }

                if (value.Score < 0m || value.Score > question.Points)
                {
                    return Results.BadRequest(new { error = "A question score must be between zero and its points." });
                }

                if (value.RubricCriterionId is { } criterionId &&
                    (rubric is null || rubric.Criteria.All(criterion => criterion.Id != criterionId)))
                {
                    return Results.BadRequest(new { error = "The selected rubric criterion is not available." });
                }

                var criterion = value.RubricCriterionId is { } selectedCriterion && rubric is not null
                    ? rubric.Criteria.Single(criterion => criterion.Id == selectedCriterion)
                    : null;
                if (criterion is not null && value.Score > criterion.MaxPoints)
                {
                    return Results.BadRequest(new { error = "A question score exceeds the rubric criterion." });
                }
            }

            foreach (var question in activity.Questions.Where(question => !GradingRules.IsObjective(question.Type)))
            {
                if (!suppliedById.ContainsKey(question.Id))
                {
                    return Results.BadRequest(new { error = "Every subjective question needs a teacher score." });
                }
            }

            var automaticByQuestion = entry.QuestionResults.ToDictionary(result => result.QuestionId);
            entry.QuestionResults = activity.Questions.OrderBy(question => question.Order).Select(question =>
            {
                if (suppliedById.TryGetValue(question.Id, out var value))
                {
                    return new GradeQuestionResult(question.Id, value.Score, question.Points,
                        GradingRules.NormalizeFeedback(value.Feedback), value.RubricCriterionId);
                }

                return automaticByQuestion.TryGetValue(question.Id, out var automatic)
                    ? automatic
                    : new GradeQuestionResult(question.Id, 0m, question.Points, null, null);
            }).ToArray();
            entry.Score = entry.QuestionResults.Sum(result => result.Score);
            entry.MaxScore = item.MaxScore;
            entry.Feedback = GradingRules.NormalizeFeedback(request.Feedback);
            entry.Source = GradeSource.Manual;
            entry.IsComplete = true;
            entry.Status = GradeEntryStatus.Draft;
            entry.GradedBy = actor!.UserId;
            entry.GradedAt = DateTimeOffset.UtcNow;
            entry.PublishedAt = null;
            entry.Version++;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            session.Store(entry);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(GradeEntryResponse.From(entry));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
