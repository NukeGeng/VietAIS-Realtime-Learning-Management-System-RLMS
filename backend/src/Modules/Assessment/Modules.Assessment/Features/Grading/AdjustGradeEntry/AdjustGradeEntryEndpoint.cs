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

namespace LearningPlatform.Assessment.Features.Grading.AdjustGradeEntry;

internal static class AdjustGradeEntryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/grade-entries/{gradeEntryId:guid}/adjust",
                AdjustGradeEntryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
    }


    private static async Task<IResult> AdjustGradeEntryAsync(Guid gradeEntryId,
        AdjustGradeEntryRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
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

        if (entry.Status != GradeEntryStatus.Published)
        {
            return Results.Conflict(new { error = "Only a published grade can be adjusted." });
        }

        if (request.Score < 0m || request.Score > entry.MaxScore)
        {
            return Results.BadRequest(new { error = "The adjusted score must be within the grade max score." });
        }

        string reason;
        string? feedback;
        try
        {
            reason = GradingRules.RequireAdjustmentReason(request.Reason);
            feedback = GradingRules.NormalizeFeedback(request.Feedback);
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }

        var now = DateTimeOffset.UtcNow;
        var previousScore = entry.Score;
        var previousFeedback = entry.Feedback;
        entry.Score = request.Score;
        entry.Feedback = feedback;
        entry.Source = GradeSource.Manual;
        entry.IsComplete = true;
        entry.Status = GradeEntryStatus.Draft;
        entry.GradedBy = actor!.UserId;
        entry.GradedAt = now;
        entry.PublishedAt = null;
        entry.Passed = null;
        AssessmentActivity? activity = null;
        if (entry.ActivityId != Guid.Empty)
        {
            activity = await session.LoadAsync<AssessmentActivity>(entry.ActivityId,
                cancellationToken);
            if (activity is not null)
            {
                entry.Passed = activity.ExamPolicy?.PassingScore is { } passingScore
                    ? entry.MaxScore != 0m && entry.Score * 100m / entry.MaxScore >= passingScore
                    : null;
            }
        }

        entry.Version++;
        entry.UpdatedAt = now;
        session.Store(entry);
        session.Insert(new GradeAdjustment
        {
            Id = Guid.NewGuid(),
            OrganizationId = entry.OrganizationId,
            LearningClassId = entry.LearningClassId,
            GradeEntryId = entry.Id,
            GradeItemId = entry.GradeItemId,
            StudentId = entry.StudentId,
            PreviousScore = previousScore,
            NewScore = entry.Score,
            MaxScore = entry.MaxScore,
            PreviousFeedback = previousFeedback,
            NewFeedback = entry.Feedback,
            Reason = reason,
            AdjustedBy = actor.UserId,
            AdjustedAt = now,
            GradeVersion = entry.Version
        });
        await session.SaveChangesAsync(cancellationToken);

        var gradeItem = await session.LoadAsync<GradeItem>(entry.GradeItemId, cancellationToken);
        var effective = await session.Query<GradeEntry>()
            .Where(value => value.GradeItemId == entry.GradeItemId &&
                value.StudentId == entry.StudentId && value.Status == GradeEntryStatus.Published)
            .OrderByDescending(value => value.AttemptNumber)
            .ThenByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var learningClass = await academics.FindClassAsync(actor.OrganizationId,
            entry.LearningClassId, cancellationToken);
        var effectiveActivity = effective?.ActivityId is { } effectiveActivityId &&
            effectiveActivityId != Guid.Empty
            ? await session.LoadAsync<AssessmentActivity>(effectiveActivityId, cancellationToken)
            : activity;
        var effectiveActivityIdForProgress = effective?.EssayAssignmentId ?? effective?.ActivityId ??
            entry.EssayAssignmentId ?? entry.ActivityId;
        var effectiveGradeEntryId = effective?.Id ?? entry.Id;
        var effectiveTitle = effective is null
            ? activity?.Title ?? gradeItem?.Title ?? ""
            : gradeItem?.Title ?? effectiveActivity?.Title ?? "";
        var effectiveAttemptNumber = effective?.AttemptNumber ?? entry.AttemptNumber;
        var effectiveScore = effective?.Score ?? entry.Score;
        var effectiveMaxScore = effective?.MaxScore ?? entry.MaxScore;
        var effectiveVersion = effective?.Version ?? entry.Version;
        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Grading");
        await GradePublicationPublisher.PublishVisibilityChangedAsync(messageBus, logger,
            new GradeEntryVisibilityChanged(entry.OrganizationId, entry.LearningClassId,
                gradeItem?.CourseId ?? learningClass?.CourseId, entry.StudentId,
                effectiveActivityIdForProgress, effectiveGradeEntryId, effectiveTitle,
                effective is not null, effectiveAttemptNumber, effectiveScore, effectiveMaxScore,
                effectiveVersion, now, effectiveActivity?.ExamPolicy?.ResultsReleaseAt));
        return Results.Ok(GradeEntryResponse.From(entry));
    }

}
