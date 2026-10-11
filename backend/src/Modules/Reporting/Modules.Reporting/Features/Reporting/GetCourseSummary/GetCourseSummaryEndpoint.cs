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

namespace LearningPlatform.Reporting.Features.Reporting.GetCourseSummary;

internal static class GetCourseSummaryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/courses/{courseId:guid}/summary", GetCourseSummaryAsync)
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

}
