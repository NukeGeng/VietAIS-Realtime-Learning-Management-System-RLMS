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

namespace LearningPlatform.Assessment.Features.Grading.PublishGradeEntry;

internal static class PublishGradeEntryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/grade-entries/{gradeEntryId:guid}/publish",
                PublishGradeEntryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
    }


    private static async Task<IResult> PublishGradeEntryAsync(Guid gradeEntryId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
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

        var activity = await session.LoadAsync<AssessmentActivity>(entry.ActivityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!entry.IsComplete)
        {
            return Results.Conflict(new { error = "The grade must be complete before publication." });
        }

        if (entry.Status != GradeEntryStatus.Published)
        {
            var now = DateTimeOffset.UtcNow;
            entry.Status = GradeEntryStatus.Published;
            entry.PublishedAt = now;
            entry.Passed = activity.ExamPolicy?.PassingScore is { } passingScore
                ? entry.MaxScore != 0m && entry.Score * 100m / entry.MaxScore >= passingScore
                : null;
            entry.UpdatedAt = now;
            entry.Version++;
            session.Store(entry);
            await session.SaveChangesAsync(cancellationToken);

            var learningClass = await academics.FindClassAsync(actor!.OrganizationId,
                entry.LearningClassId, cancellationToken);
            var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Grading");
            await GradePublicationPublisher.PublishAsync(messageBus, logger,
                new GradeEntryPublished(entry.OrganizationId, entry.LearningClassId,
                    learningClass?.CourseId, entry.StudentId, entry.ActivityId, entry.GradeItemId,
                    entry.Id, activity.Title, entry.AttemptNumber, entry.Score, entry.MaxScore,
                    entry.Version, now, activity.ExamPolicy?.ResultsReleaseAt));
        }

        return Results.Ok(GradeEntryResponse.From(entry));
    }

}
