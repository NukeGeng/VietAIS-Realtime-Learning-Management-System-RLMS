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

namespace LearningPlatform.Reporting.Features.Reporting.GetEssaySummary;

internal static class GetEssaySummaryEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/essay-assignments/{assignmentId:guid}/summary",
                GetEssaySummaryAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadReports);
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

}
