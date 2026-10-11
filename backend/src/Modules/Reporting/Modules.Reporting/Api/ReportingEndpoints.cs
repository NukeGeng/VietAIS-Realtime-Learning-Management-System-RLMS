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
        LearningPlatform.Reporting.Features.Reporting.GetClassSummary.GetClassSummaryEndpoint.Map(endpoints);
        LearningPlatform.Reporting.Features.Reporting.GetCourseSummary.GetCourseSummaryEndpoint.Map(endpoints);
        LearningPlatform.Reporting.Features.Reporting.GetOwnStudentReport.GetOwnStudentReportEndpoint.Map(endpoints);
        LearningPlatform.Reporting.Features.Reporting.GetStudentReport.GetStudentReportEndpoint.Map(endpoints);
        LearningPlatform.Reporting.Features.Reporting.GetActivitySummary.GetActivitySummaryEndpoint.Map(endpoints);
        LearningPlatform.Reporting.Features.Reporting.GetEssaySummary.GetEssaySummaryEndpoint.Map(endpoints);
    }

    internal static async Task<List<LearningClass>> FilterReadableClassesAsync(
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


    internal static async Task<bool> StudentReportNeedsRefreshAsync(IQuerySession session,
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


    internal static async Task<bool> CanReadClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }

}
