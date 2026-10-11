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

namespace LearningPlatform.Reporting.Features.Reporting.GetClassSummary;

internal static class GetClassSummaryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/classes/{classId:guid}/summary", GetClassSummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
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

}
