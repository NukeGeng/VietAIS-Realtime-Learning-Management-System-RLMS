using LearningPlatform.Reporting.Api;
using static LearningPlatform.Reporting.Features.Reporting.GetStudentReport.GetStudentReportEndpoint;
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

namespace LearningPlatform.Reporting.Features.Reporting.GetOwnStudentReport;

internal static class GetOwnStudentReportEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/reporting/classes/{classId:guid}/students/me", GetOwnStudentReportAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }


    private static Task<IResult> GetOwnStudentReportAsync(Guid classId, HttpContext context,
        IQuerySession session, IDocumentStore store, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return GetStudentReportAsync(classId, Guid.Empty, context, session, store, academics,
            cancellationToken);
    }

}
