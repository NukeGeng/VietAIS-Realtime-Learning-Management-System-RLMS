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

namespace LearningPlatform.Assessment.Features.Grading.GetStudentGradebook;

internal static class GetStudentGradebookEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/gradebook/me", GetStudentGradebookAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
    }


    private static Task<IResult> GetStudentGradebookAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, IIdentityDirectory identity, CancellationToken cancellationToken)
    {
        return GetGradebookAsync(classId, Guid.Empty, context, session, academics, identity, cancellationToken);
    }

}
