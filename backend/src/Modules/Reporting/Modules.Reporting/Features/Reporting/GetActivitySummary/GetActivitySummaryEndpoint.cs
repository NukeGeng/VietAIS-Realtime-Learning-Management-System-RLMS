using LearningPlatform.Reporting.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Reporting.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static LearningPlatform.Reporting.Api.ReportingEndpoints;

namespace LearningPlatform.Reporting.Features.Reporting.GetActivitySummary;

internal static class GetActivitySummaryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/activities/{activityId:guid}/summary", GetActivitySummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
    }


    private static async Task<IResult> GetActivitySummaryAsync(Guid activityId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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

        if (!await CanReadClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var enrolled = await session.Query<Enrollment>()
            .Where(value => value.LearningClassId == activity.LearningClassId &&
                value.Status == EnrollmentStatus.Active)
            .CountAsync(cancellationToken);
        var submissions = await session.Query<Submission>()
            .Where(value => value.ActivityId == activityId)
            .ToListAsync(cancellationToken);
        var gradeItem = await session.Query<GradeItem>()
            .FirstOrDefaultAsync(value => value.ActivityId == activityId, cancellationToken);
        var publishedGrades = gradeItem is null
            ? []
            : await session.Query<GradeEntry>()
                .Where(value => value.GradeItemId == gradeItem.Id &&
                    value.Status == GradeEntryStatus.Published)
                .ToListAsync(cancellationToken);
        publishedGrades = publishedGrades
            .GroupBy(value => value.StudentId)
            .Select(group => group.OrderByDescending(value => value.AttemptNumber)
                .ThenByDescending(value => value.Version)
                .First())
            .ToList();
        var questionReports = activity.Questions.OrderBy(value => value.Order).Select(question =>
        {
            var answers = submissions.SelectMany(submission => submission.Answers)
                .Where(answer => answer.QuestionId == question.Id)
                .Select(answer => answer.Value)
                .ToArray();
            var correct = GradingRules.IsObjective(question.Type)
                ? answers.Count(value => GradingRules.IsCorrect(question, value))
                : 0;
            return new QuestionReportResponse(question.Id, question.Prompt, answers.Length, correct,
                answers.Length == 0 ? 0m : decimal.Round(correct * 100m / answers.Length, 2));
        }).ToArray();
        return Results.Ok(new ActivityReportResponse(activity.Id, activity.LearningClassId,
            activity.Title, enrolled, submissions.Count, publishedGrades.Count, questionReports));
    }

}
