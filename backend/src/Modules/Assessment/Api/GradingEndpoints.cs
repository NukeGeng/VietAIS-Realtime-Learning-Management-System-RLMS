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

namespace LearningPlatform.Assessment.Api;

public static class GradingEndpoints
{
    public static void MapGradingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}/rubric", GetRubricAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/activities/{activityId:guid}/rubric", CreateRubricAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapGet("/api/assessment/submissions/{submissionId:guid}/grade", GetGradeEntryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
        endpoints.MapPost("/api/assessment/submissions/{submissionId:guid}/grade", GradeSubmissionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
        endpoints.MapPost("/api/assessment/grade-entries/{gradeEntryId:guid}/publish",
                PublishGradeEntryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
        endpoints.MapPut("/api/assessment/grade-items/{gradeItemId:guid}", UpdateGradeItemAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
        endpoints.MapPost("/api/assessment/grade-entries/{gradeEntryId:guid}/adjust",
                AdjustGradeEntryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
        endpoints.MapGet("/api/assessment/grade-entries/{gradeEntryId:guid}/adjustments",
                ListGradeAdjustmentsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
        endpoints.MapGet("/api/assessment/activities/{activityId:guid}/result", GetStudentResultAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/gradebook", GetTeacherGradebookAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/gradebook/me", GetStudentGradebookAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
    }

    private static async Task<IResult> GetRubricAsync(Guid activityId, HttpContext context,
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

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var rubric = await session.LoadAsync<AssessmentRubric>(AssessmentConstants.RubricId(activityId),
            cancellationToken);
        return rubric is null ? Results.NotFound() : Results.Ok(RubricResponse.From(rubric));
    }

    private static async Task<IResult> CreateRubricAsync(Guid activityId,
        CreateRubricRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        var existingGrades = await session.Query<GradeEntry>()
            .Where(entry => entry.ActivityId == activityId && entry.Status == GradeEntryStatus.Published)
            .AnyAsync(cancellationToken);
        if (existingGrades)
        {
            return Results.Conflict(new { error = "A rubric cannot change after a result is published." });
        }

        try
        {
            var criteria = (request.Criteria ?? []).Select((criterion, index) => new RubricCriterion(
                    criterion.Id == Guid.Empty ? Guid.NewGuid() : criterion.Id,
                    criterion.Name?.Trim() ?? "", criterion.Description?.Trim() ?? "", criterion.MaxPoints,
                    criterion.Order < 0 ? index : criterion.Order))
                .ToArray();
            GradingRules.ValidateRubric(criteria);
            var now = DateTimeOffset.UtcNow;
            var rubric = await session.LoadAsync<AssessmentRubric>(AssessmentConstants.RubricId(activityId),
                cancellationToken);
            if (rubric is null)
            {
                rubric = new AssessmentRubric
                {
                    Id = AssessmentConstants.RubricId(activityId),
                    OrganizationId = actor!.OrganizationId,
                    ActivityId = activityId,
                    LearningClassId = activity.LearningClassId,
                    CreatedBy = actor.UserId,
                    CreatedAt = now
                };
                session.Insert(rubric);
                rubric.Criteria = criteria;
                rubric.UpdatedAt = now;
                await session.SaveChangesAsync(cancellationToken);
                return Results.Ok(RubricResponse.From(rubric));
            }

            rubric.Criteria = criteria;
            rubric.UpdatedAt = now;
            session.Store(rubric);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(RubricResponse.From(rubric));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetGradeEntryAsync(Guid submissionId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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
        if (activity is null || !await CanManageClassAsync(actor!, activity.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var entry = await session.LoadAsync<GradeEntry>(
            AssessmentConstants.GradeEntryId(activity.Id, submission.StudentId, submission.AttemptNumber),
            cancellationToken);
        return entry is null ? Results.NotFound() : Results.Ok(GradeEntryResponse.From(entry));
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

    private static async Task<IResult> UpdateGradeItemAsync(Guid gradeItemId,
        UpdateGradeItemRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var item = await session.LoadAsync<GradeItem>(gradeItemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, item.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (item.Status == GradeItemStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived grade item cannot be changed." });
        }

        try
        {
            item.Weight = GradingRules.RequireWeight(request.Weight);
            item.UpdatedAt = DateTimeOffset.UtcNow;
            session.Store(item);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(GradebookItemResponse.From(item));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> AdjustGradeEntryAsync(Guid gradeEntryId,
        AdjustGradeEntryRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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
        if (entry.ActivityId != Guid.Empty)
        {
            var activity = await session.LoadAsync<AssessmentActivity>(entry.ActivityId,
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
        return Results.Ok(GradeEntryResponse.From(entry));
    }

    private static async Task<IResult> ListGradeAdjustmentsAsync(Guid gradeEntryId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
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

        var adjustments = await session.Query<GradeAdjustment>()
            .Where(adjustment => adjustment.GradeEntryId == gradeEntryId)
            .OrderByDescending(adjustment => adjustment.AdjustedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(adjustments.Select(GradeAdjustmentResponse.From));
    }

    private static async Task<IResult> GetStudentResultAsync(Guid activityId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null || !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                activity.LearningClassId, actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!AssessmentRules.IsResultsReleased(activity, DateTimeOffset.UtcNow))
        {
            // Keep unreleased exam results indistinguishable from a missing result.
            return Results.NotFound();
        }

        var entry = (await session.Query<GradeEntry>()
                .Where(value => value.ActivityId == activityId && value.StudentId == actor.UserId &&
                    value.Status == GradeEntryStatus.Published)
                .OrderByDescending(value => value.AttemptNumber)
                .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return entry is null || entry.Status != GradeEntryStatus.Published
            ? Results.NotFound()
            : Results.Ok(StudentResultResponse.From(entry));
    }

    private static Task<IResult> GetTeacherGradebookAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return GetGradebookAsync(classId, null, context, session, academics, cancellationToken);
    }

    private static Task<IResult> GetStudentGradebookAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return GetGradebookAsync(classId, Guid.Empty, context, session, academics, cancellationToken);
    }

    private static async Task<IResult> GetGradebookAsync(Guid classId, Guid? studentFilter,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var isTeacher = studentFilter is null;
        if (isTeacher)
        {
            if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
            {
                return Results.Forbid();
            }
        }
        else if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                     cancellationToken))
        {
            return Results.Forbid();
        }

        var activities = await session.Query<AssessmentActivity>()
            .Where(activity => activity.LearningClassId == classId &&
                activity.Status != AssessmentActivityStatus.Draft)
            .OrderBy(activity => activity.CreatedAt)
            .ToListAsync(cancellationToken);
        var activityIds = activities.Select(activity => activity.Id).ToArray();
        var essays = await session.Query<EssayAssignment>()
            .Where(assignment => assignment.LearningClassId == classId &&
                assignment.Status != EssayAssignmentStatus.Draft)
            .OrderBy(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        var essayAssignmentIds = essays.Select(assignment => assignment.Id).ToArray();
        var items = await session.Query<GradeItem>()
            .Where(item => item.LearningClassId == classId &&
                (activityIds.Contains(item.ActivityId) ||
                 item.EssayAssignmentId != null && essayAssignmentIds.Contains(item.EssayAssignmentId.Value)))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var entries = await session.Query<GradeEntry>()
            .Where(entry => entry.LearningClassId == classId &&
                (activityIds.Contains(entry.ActivityId) ||
                 entry.EssayAssignmentId != null && essayAssignmentIds.Contains(entry.EssayAssignmentId.Value)) &&
                (studentFilter == null || entry.StudentId == actor.UserId))
            .ToListAsync(cancellationToken);
        if (!isTeacher)
        {
            var activityById = activities.ToDictionary(activity => activity.Id);
            entries = entries.Where(entry => !activityById.TryGetValue(entry.ActivityId,
                    out var activity) || AssessmentRules.IsResultsReleased(activity,
                    DateTimeOffset.UtcNow))
                .ToList();
        }
        var itemResponses = items.Select(GradebookItemResponse.From).ToArray();
        Guid[] studentIds;
        if (studentFilter is null)
        {
            studentIds = (await session.Query<Enrollment>()
                .Where(enrollment => enrollment.LearningClassId == classId &&
                    enrollment.Status == EnrollmentStatus.Active)
                .OrderBy(enrollment => enrollment.StudentId)
                .Select(enrollment => enrollment.StudentId)
                .ToListAsync(cancellationToken)).ToArray();
        }
        else
        {
            studentIds = [actor.UserId];
        }

        var rows = studentIds.Select(studentId =>
        {
            var cells = items.Select(item =>
            {
                var entry = entries.Where(value => value.GradeItemId == item.Id &&
                        value.StudentId == studentId && (isTeacher || value.Status == GradeEntryStatus.Published))
                    .OrderByDescending(value => value.AttemptNumber)
                    .FirstOrDefault();
                return GradebookCellResponse.From(item, entry);
            }).ToArray();
            var visible = cells.Where(cell => cell.Score is not null).ToArray();
            var visibleMaxScore = visible.Sum(cell => cell.MaxScore);
            var average = visible.Length == 0 || visibleMaxScore == 0m
                ? (decimal?)null
                : visible.Sum(cell => cell.Score!.Value) / visibleMaxScore;
            var weightTotal = visible.Sum(cell => cell.Weight);
            var weightedAverage = weightTotal <= 0m
                ? (decimal?)null
                : visible.Sum(cell => cell.MaxScore == 0m
                    ? 0m
                    : cell.Score!.Value / cell.MaxScore * cell.Weight) / weightTotal;
            decimal? finalScore = weightedAverage is { } weighted
                ? decimal.Round(weighted * 100m, 2)
                : null;
            return new GradebookStudentRowResponse(studentId, average, weightedAverage, finalScore, cells);
        }).ToArray();
        return Results.Ok(new GradebookResponse(classId, itemResponses, rows));
    }

    private static async Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }
}

public sealed record CreateRubricRequest(RubricCriterionRequest[]? Criteria);

public sealed record RubricCriterionRequest(Guid Id, string Name, string? Description, decimal MaxPoints,
    int Order);

public sealed record RubricResponse(Guid Id, Guid ActivityId, RubricCriterion[] Criteria,
    DateTimeOffset UpdatedAt)
{
    public static RubricResponse From(AssessmentRubric rubric) => new(rubric.Id, rubric.ActivityId,
        rubric.Criteria, rubric.UpdatedAt);
}

public sealed record GradeSubmissionRequest(GradeQuestionRequest[]? Questions, string? Feedback,
    Guid? RubricId);

public sealed record GradeQuestionRequest(Guid QuestionId, decimal Score, string? Feedback,
    Guid? RubricCriterionId);

public sealed record GradeEntryResponse(Guid Id, Guid ActivityId, Guid StudentId, int AttemptNumber,
    Guid GradeItemId, Guid? SourceSubmissionId, decimal Score, decimal MaxScore, GradeEntryStatus Status,
    GradeSource Source, bool IsComplete, string? Feedback, GradeQuestionResult[] QuestionResults,
    Guid? GradedBy, DateTimeOffset? GradedAt, DateTimeOffset? PublishedAt, bool? Passed, int Version)
{
    public static GradeEntryResponse From(GradeEntry entry) => new(entry.Id, entry.ActivityId,
        entry.StudentId, entry.AttemptNumber, entry.GradeItemId, entry.SourceSubmissionId,
        entry.Score, entry.MaxScore,
        entry.Status, entry.Source, entry.IsComplete, entry.Feedback, entry.QuestionResults,
        entry.GradedBy, entry.GradedAt, entry.PublishedAt, entry.Passed, entry.Version);
}

public sealed record StudentResultResponse(Guid ActivityId, Guid GradeEntryId, decimal Score,
    decimal MaxScore, string? Feedback, GradeQuestionResult[] QuestionResults,
    DateTimeOffset PublishedAt, bool? Passed, int Version)
{
    public static StudentResultResponse From(GradeEntry entry) => new(entry.ActivityId, entry.Id,
        entry.Score, entry.MaxScore, entry.Feedback, entry.QuestionResults, entry.PublishedAt!.Value,
        entry.Passed, entry.Version);
}

public sealed record GradebookResponse(Guid LearningClassId, GradebookItemResponse[] Items,
    GradebookStudentRowResponse[] Students);

public sealed record GradebookItemResponse(Guid GradeItemId, Guid ActivityId, Guid? EssayAssignmentId,
    string Title, decimal MaxScore, decimal Weight)
{
    public static GradebookItemResponse From(GradeItem item) => new(item.Id, item.ActivityId,
        item.EssayAssignmentId, item.Title, item.MaxScore, item.Weight);
}

public sealed record GradebookStudentRowResponse(Guid StudentId, decimal? Average,
    decimal? WeightedAverage, decimal? FinalScore, GradebookCellResponse[] Entries);

public sealed record GradebookCellResponse(Guid GradeItemId, decimal? Score, decimal MaxScore,
    decimal Weight, GradeEntryStatus? Status, string? Feedback, int? Version)
{
    public static GradebookCellResponse From(GradeItem item, GradeEntry? entry) =>
        entry is null ? new(item.Id, null, item.MaxScore, item.Weight, null, null, null) :
        new(item.Id, entry.Score, entry.MaxScore, item.Weight, entry.Status, entry.Feedback,
            entry.Version);
}

public sealed record UpdateGradeItemRequest(decimal Weight);

public sealed record AdjustGradeEntryRequest(decimal Score, string? Feedback, string? Reason);

public sealed record GradeAdjustmentResponse(Guid Id, Guid GradeEntryId, Guid StudentId,
    decimal PreviousScore, decimal NewScore, decimal MaxScore, string? PreviousFeedback,
    string? NewFeedback, string Reason, Guid AdjustedBy, DateTimeOffset AdjustedAt,
    int GradeVersion)
{
    public static GradeAdjustmentResponse From(GradeAdjustment adjustment) => new(adjustment.Id,
        adjustment.GradeEntryId, adjustment.StudentId, adjustment.PreviousScore,
        adjustment.NewScore, adjustment.MaxScore, adjustment.PreviousFeedback,
        adjustment.NewFeedback, adjustment.Reason, adjustment.AdjustedBy, adjustment.AdjustedAt,
        adjustment.GradeVersion);
}
