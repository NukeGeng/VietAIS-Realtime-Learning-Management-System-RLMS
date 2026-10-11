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

namespace LearningPlatform.Reporting.Features.Reporting.GetStudentReport;

internal static class GetStudentReportEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/classes/{classId:guid}/students/{studentId:guid}",
                GetStudentReportAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadStudentReports);
    }


    internal static async Task<IResult> GetStudentReportAsync(Guid classId, Guid studentId,
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

}
