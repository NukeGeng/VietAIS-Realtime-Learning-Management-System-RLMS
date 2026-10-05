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

namespace LearningPlatform.Reporting.Api;

public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/classes/{classId:guid}/summary", GetClassSummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
        endpoints.MapGet("/api/reporting/courses/{courseId:guid}/summary", GetCourseSummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
        endpoints.MapGet("/api/reporting/classes/{classId:guid}/students/me", GetOwnStudentReportAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapGet("/api/reporting/classes/{classId:guid}/students/{studentId:guid}",
                GetStudentReportAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadStudentReports);
        endpoints.MapGet("/api/reporting/activities/{activityId:guid}/summary", GetActivitySummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
        endpoints.MapGet("/api/reporting/essay-assignments/{assignmentId:guid}/summary",
                GetEssaySummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
    }

    private static async Task<IResult> GetCourseSummaryAsync(Guid courseId, HttpContext context,
        IQuerySession session, IDocumentStore store, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
        {
            return Results.NotFound();
        }

        var allClasses = await session.Query<LearningClass>()
            .Where(value => value.CourseId == courseId)
            .OrderBy(value => value.StartDate)
            .ThenBy(value => value.Name)
            .ToListAsync(cancellationToken);
        var classes = actor!.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal) || course.CreatedBy == actor.UserId
            ? allClasses
            : await FilterReadableClassesAsync(allClasses, actor, session, cancellationToken);
        if (classes.Count == 0)
        {
            return Results.Forbid();
        }

        var reports = new List<ClassReportResponse>(classes.Count);
        foreach (var learningClass in classes)
        {
            var snapshot = await session.LoadAsync<ClassReportSnapshot>(
                ReportingConstants.ClassSnapshotId(learningClass.Id), cancellationToken);
            if (snapshot is null)
            {
                await ReportingProjectionHandler.RebuildClassAsync(store, actor.OrganizationId,
                    learningClass.Id, cancellationToken);
                snapshot = await session.LoadAsync<ClassReportSnapshot>(
                    ReportingConstants.ClassSnapshotId(learningClass.Id), cancellationToken);
            }

            reports.Add(snapshot is null
                ? ClassReportResponse.Empty(learningClass)
                : ClassReportResponse.From(snapshot));
        }

        var publishedGrades = reports.Sum(report => report.PublishedGradeCount);
        var weightedScore = reports.Sum(report => report.AverageScorePercent *
            report.PublishedGradeCount);
        var response = new CourseReportResponse(course.Id, course.Code, course.Title,
            reports.Count, reports.Sum(report => report.EnrollmentCount),
            reports.Sum(report => report.ActiveEnrollmentCount),
            reports.Sum(report => report.ActivityCount), reports.Sum(report => report.SubmissionCount),
            publishedGrades, publishedGrades == 0 ? 0m : decimal.Round(weightedScore / publishedGrades, 2),
            reports.ToArray());
        return Results.Ok(response);
    }

    private static async Task<IResult> GetClassSummaryAsync(Guid classId, HttpContext context,
        IQuerySession session, IDocumentStore store, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanReadClassAsync(actor!, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var snapshot = await session.LoadAsync<ClassReportSnapshot>(
            ReportingConstants.ClassSnapshotId(classId), cancellationToken);
        if (snapshot is null)
        {
            // The first read can repair a cold projection. Subsequent reads use the
            // durable snapshot maintained by the asynchronous projection queue.
            await ReportingProjectionHandler.RebuildClassAsync(store, actor!.OrganizationId, classId,
                cancellationToken);
            snapshot = await session.LoadAsync<ClassReportSnapshot>(
                ReportingConstants.ClassSnapshotId(classId), cancellationToken);
        }
        return Results.Ok(snapshot is null
            ? ClassReportResponse.Empty(learningClass)
            : ClassReportResponse.From(snapshot));
    }

    private static async Task<List<LearningClass>> FilterReadableClassesAsync(
        IReadOnlyList<LearningClass> classes, AuthenticatedActor actor, IQuerySession session,
        CancellationToken cancellationToken)
    {
        var classIds = classes.Select(value => value.Id).ToArray();
        var assigned = classIds.Length == 0
            ? []
            : await session.Query<ClassTeacherAssignment>()
                .Where(value => classIds.Contains(value.LearningClassId) &&
                    value.TeacherId == actor.UserId && value.Status == TeacherAssignmentStatus.Active)
                .Select(value => value.LearningClassId)
                .ToListAsync(cancellationToken);
        var created = classes.Where(value => value.CreatedBy == actor.UserId)
            .Select(value => value.Id);
        var readable = assigned.Concat(created).ToHashSet();
        return classes.Where(value => readable.Contains(value.Id)).ToList();
    }

    private static Task<IResult> GetOwnStudentReportAsync(Guid classId, HttpContext context,
        IQuerySession session, IDocumentStore store, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return GetStudentReportAsync(classId, Guid.Empty, context, session, store, academics,
            cancellationToken);
    }

    private static async Task<IResult> GetStudentReportAsync(Guid classId, Guid studentId,
        HttpContext context, IQuerySession session, IDocumentStore store, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanReadClassAsync(actor!, classId, academics, cancellationToken);
        var requestedStudentId = studentId == Guid.Empty ? actor!.UserId : studentId;
        if (!canManage && requestedStudentId != actor!.UserId)
        {
            return Results.Forbid();
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, classId, requestedStudentId,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var snapshot = await session.LoadAsync<StudentReportSnapshot>(
            ReportingConstants.StudentSnapshotId(classId, requestedStudentId), cancellationToken);
        if (snapshot is null || await StudentReportNeedsRefreshAsync(session, classId,
                requestedStudentId, snapshot, cancellationToken))
        {
            await ReportingProjectionHandler.RebuildStudentAsync(store, actor.OrganizationId, classId,
                requestedStudentId, cancellationToken);
            snapshot = await session.LoadAsync<StudentReportSnapshot>(
                ReportingConstants.StudentSnapshotId(classId, requestedStudentId), cancellationToken);
        }
        return Results.Ok(snapshot is null
            ? StudentReportResponse.Empty(classId, requestedStudentId)
            : StudentReportResponse.From(snapshot));
    }

    private static async Task<bool> StudentReportNeedsRefreshAsync(IQuerySession session,
        Guid classId, Guid studentId, StudentReportSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activities = await session.Query<AssessmentActivity>()
            .Where(value => value.LearningClassId == classId &&
                value.Status != AssessmentActivityStatus.Draft)
            .ToListAsync(cancellationToken);
        var released = activities
            .Where(value => value.ExamPolicy?.ResultsReleaseAt is { } releaseAt && releaseAt <= now)
            .ToArray();
        if (released.Length == 0)
        {
            return false;
        }

        var releasedIds = released.Select(value => value.Id).ToArray();
        var transitionedIds = released
            .Where(value => value.ExamPolicy!.ResultsReleaseAt > snapshot.UpdatedAt)
            .Select(value => value.Id)
            .ToArray();
        return await session.Query<GradeEntry>()
            .Where(value => value.LearningClassId == classId && value.StudentId == studentId &&
                releasedIds.Contains(value.ActivityId) &&
                (value.UpdatedAt > snapshot.UpdatedAt || transitionedIds.Contains(value.ActivityId)))
            .AnyAsync(cancellationToken);
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

    private static async Task<IResult> GetEssaySummaryAsync(Guid assignmentId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanReadClassAsync(actor!, assignment.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var enrolled = await session.Query<Enrollment>()
            .Where(value => value.LearningClassId == assignment.LearningClassId &&
                value.Status == EnrollmentStatus.Active)
            .CountAsync(cancellationToken);
        var groups = await session.Query<EssayGroup>()
            .Where(value => value.EssayAssignmentId == assignmentId &&
                value.Status == EssayGroupStatus.Active)
            .CountAsync(cancellationToken);
        var submissions = await session.Query<EssaySubmission>()
            .Where(value => value.EssayAssignmentId == assignmentId &&
                value.Status != EssaySubmissionStatus.Rejected)
            .CountAsync(cancellationToken);
        var grades = await session.Query<GradeEntry>()
            .Where(value => value.EssayAssignmentId == assignmentId &&
                value.Status == GradeEntryStatus.Published)
            .CountAsync(cancellationToken);
        return Results.Ok(new EssayReportResponse(assignment.Id, assignment.LearningClassId,
            assignment.Title, enrolled, groups, submissions, grades));
    }

    private static async Task<bool> CanReadClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }
}

public sealed record ClassReportResponse(Guid LearningClassId, Guid CourseId, int EnrollmentCount,
    int ActiveEnrollmentCount, int CompletedEnrollmentCount, int ActivityCount,
    int PublishedActivityCount, int SubmissionCount, int PublishedGradeCount,
    decimal AverageScorePercent, int AttendanceSessionCount, int AttendancePresentCount,
    int AttendanceLateCount, int AttendanceAbsentCount, int AttendanceExcusedCount,
    decimal AttendancePercentage, int OverviewStudentCount, int OverviewInProgressCount,
    int OverviewSubmittedCount, int OverviewHelpRequestedCount, long Version,
    DateTimeOffset UpdatedAt)
{
    public static ClassReportResponse From(ClassReportSnapshot snapshot) => new(snapshot.LearningClassId,
        snapshot.CourseId, snapshot.EnrollmentCount, snapshot.ActiveEnrollmentCount,
        snapshot.CompletedEnrollmentCount, snapshot.ActivityCount, snapshot.PublishedActivityCount,
        snapshot.SubmissionCount, snapshot.PublishedGradeCount, snapshot.AverageScorePercent,
        snapshot.AttendanceSessionCount, snapshot.AttendancePresentCount, snapshot.AttendanceLateCount,
        snapshot.AttendanceAbsentCount, snapshot.AttendanceExcusedCount, snapshot.AttendancePercentage,
        snapshot.OverviewStudentCount, snapshot.OverviewInProgressCount, snapshot.OverviewSubmittedCount,
        snapshot.OverviewHelpRequestedCount, snapshot.Version, snapshot.UpdatedAt);

    public static ClassReportResponse Empty(LearningClass learningClass) => new(learningClass.Id,
        learningClass.CourseId, 0, 0, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0, 0,
        DateTimeOffset.UtcNow);
}

public sealed record StudentReportResponse(Guid LearningClassId, Guid StudentId, int PublishedGradeCount,
    decimal AverageScorePercent, int CompletedActivityCount, int ActivityCount,
    decimal ActivityCompletionPercent, int LessonCount, int CompletedLessonCount,
    decimal LessonCompletionPercent, int AttendanceSessionCount, int AttendancePresentCount,
    int AttendanceLateCount, int AttendanceAbsentCount, int AttendanceExcusedCount,
    decimal AttendancePercentage, long Version, DateTimeOffset UpdatedAt)
{
    public static StudentReportResponse From(StudentReportSnapshot snapshot) => new(snapshot.LearningClassId,
        snapshot.StudentId, snapshot.PublishedGradeCount, snapshot.AverageScorePercent,
        snapshot.CompletedActivityCount, snapshot.ActivityCount, snapshot.ActivityCompletionPercent,
        snapshot.LessonCount, snapshot.CompletedLessonCount, snapshot.LessonCompletionPercent,
        snapshot.AttendanceSessionCount, snapshot.AttendancePresentCount, snapshot.AttendanceLateCount,
        snapshot.AttendanceAbsentCount, snapshot.AttendanceExcusedCount, snapshot.AttendancePercentage,
        snapshot.Version, snapshot.UpdatedAt);

    public static StudentReportResponse Empty(Guid classId, Guid studentId) => new(classId,
        studentId, 0, 0m, 0, 0, 0m, 0, 0, 0m, 0, 0, 0, 0, 0, 0m, 0,
        DateTimeOffset.UtcNow);
}

public sealed record CourseReportResponse(Guid CourseId, string Code, string Title, int ClassCount,
    int EnrollmentCount, int ActiveEnrollmentCount, int ActivityCount, int SubmissionCount,
    int PublishedGradeCount, decimal AverageScorePercent, ClassReportResponse[] Classes);

public sealed record EssayReportResponse(Guid AssignmentId, Guid LearningClassId, string Title,
    int EnrolledStudentCount, int ActiveGroupCount, int SubmissionCount,
    int PublishedGradeCount);

public sealed record ActivityReportResponse(Guid ActivityId, Guid LearningClassId, string Title,
    int EnrolledStudentCount, int SubmissionCount, int PublishedGradeCount,
    QuestionReportResponse[] Questions);

public sealed record QuestionReportResponse(Guid QuestionId, string Prompt, int AnswerCount,
    int CorrectAnswerCount, decimal CorrectnessPercent);
