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

namespace LearningPlatform.Assessment.Features.Essay.GradeEssaySubmission;

internal static class GradeEssaySubmissionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-submissions/{submissionId:guid}/grade",
                GradeEssaySubmissionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
    }


    private static async Task<IResult> GradeEssaySubmissionAsync(Guid submissionId,
        GradeEssaySubmissionRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var submission = await session.LoadAsync<EssaySubmission>(submissionId, cancellationToken);
        if (submission is null)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(submission.EssayAssignmentId,
            cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, assignment.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (submission.Status == EssaySubmissionStatus.Rejected)
        {
            return Results.Conflict(new { error = "A rejected report cannot be graded." });
        }

        var latestSubmission = await session.Query<EssaySubmission>()
            .Where(value => value.EssayGroupId == submission.EssayGroupId &&
                value.Status != EssaySubmissionStatus.Rejected)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestSubmission?.Id != submission.Id)
        {
            return Results.Conflict(new { error = "Only the latest report version can be graded." });
        }

        if (request.Score < 0m || request.Score > assignment.MaxScore)
        {
            return Results.BadRequest(new { error = "The essay score must be within the assignment points." });
        }

        var now = DateTimeOffset.UtcNow;
        var gradeItem = await session.LoadAsync<GradeItem>(EssayConstants.GradeItemId(assignment.Id),
            cancellationToken);
        if (gradeItem is null)
        {
            gradeItem = new GradeItem
            {
                Id = EssayConstants.GradeItemId(assignment.Id),
                OrganizationId = assignment.OrganizationId,
                LearningClassId = assignment.LearningClassId,
                ActivityId = Guid.Empty,
                EssayAssignmentId = assignment.Id,
                CourseId = assignment.CourseId,
                Title = assignment.Title,
                MaxScore = assignment.MaxScore,
                Weight = 1m,
                Status = GradeItemStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(gradeItem);
        }

        var group = await session.LoadAsync<EssayGroup>(submission.EssayGroupId, cancellationToken);
        var members = group?.Members.OrderBy(member => member.JoinedAt).ToArray() ?? [];
        if (members.Length == 0)
        {
            return Results.Conflict(new { error = "The essay group has no active member." });
        }

        GradeEntry? representative = null;
        foreach (var member in members)
        {
            var entry = await session.LoadAsync<GradeEntry>(EssayConstants.GradeEntryId(assignment.Id,
                submission.EssayGroupId, member.StudentId), cancellationToken) ?? new GradeEntry
                {
                    Id = EssayConstants.GradeEntryId(assignment.Id, submission.EssayGroupId,
                        member.StudentId),
                    OrganizationId = assignment.OrganizationId,
                    LearningClassId = assignment.LearningClassId,
                    ActivityId = Guid.Empty,
                    EssayAssignmentId = assignment.Id,
                    EssayGroupId = submission.EssayGroupId,
                    GradeItemId = gradeItem.Id,
                    StudentId = member.StudentId,
                    SourceSubmissionId = submission.Id,
                    AttemptNumber = 1,
                    MaxScore = assignment.MaxScore,
                    Version = 0
                };
            entry.Score = request.Score;
            entry.MaxScore = assignment.MaxScore;
            entry.Status = GradeEntryStatus.Draft;
            entry.Source = GradeSource.Manual;
            entry.IsComplete = true;
            entry.Feedback = GradingRules.NormalizeFeedback(request.Feedback);
            entry.GradedBy = actor!.UserId;
            entry.GradedAt = now;
            entry.PublishedAt = null;
            entry.Passed = null;
            entry.SourceSubmissionId = submission.Id;
            entry.Version++;
            entry.UpdatedAt = now;
            session.Store(entry);
            representative ??= entry;
        }
        submission.Status = EssaySubmissionStatus.Graded;
        session.Store(submission);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(GradeEntryResponse.From(representative!));
    }

}
