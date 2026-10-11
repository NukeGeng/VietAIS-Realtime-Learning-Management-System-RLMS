using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Storage;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Api.EssayEndpoints;

namespace LearningPlatform.Assessment.Features.Essay.PublishEssayGrade;

internal static class PublishEssayGradeEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-grade-entries/{gradeEntryId:guid}/publish",
                PublishEssayGradeAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
    }


    private static async Task<IResult> PublishEssayGradeAsync(Guid gradeEntryId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var entry = await session.LoadAsync<GradeEntry>(gradeEntryId, cancellationToken);
        if (entry is null || entry.EssayAssignmentId is not { } assignmentId ||
            entry.EssayGroupId is not { } groupId)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        if (assignment is null || !await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (!entry.IsComplete)
        {
            return Results.Conflict(new { error = "The essay grade must be complete before publication." });
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var entries = await session.Query<GradeEntry>()
            .Where(value => value.EssayAssignmentId == assignmentId && value.EssayGroupId == groupId)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            entries = [entry];
        }

        if (entries.Any(value => !value.IsComplete))
        {
            return Results.Conflict(new { error = "Every group member grade must be complete before publication." });
        }

        var toPublish = entries.Where(value => value.Status != GradeEntryStatus.Published).ToArray();
        if (toPublish.Length > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var value in toPublish)
            {
                value.Status = GradeEntryStatus.Published;
                value.PublishedAt = now;
                value.UpdatedAt = now;
                value.Version++;
                session.Store(value);
            }
            await session.SaveChangesAsync(cancellationToken);
            var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.EssayGrading");
            foreach (var value in toPublish)
            {
                await GradePublicationPublisher.PublishAsync(messageBus, logger,
                    new GradeEntryPublished(value.OrganizationId, value.LearningClassId,
                        assignment.CourseId, value.StudentId, assignment.Id, value.GradeItemId, value.Id,
                        assignment.Title, value.AttemptNumber, value.Score, value.MaxScore, value.Version,
                        now));
            }
        }

        return Results.Ok(GradeEntryResponse.From(entries.Single(value => value.Id == gradeEntryId)));
    }

}
