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

namespace LearningPlatform.Assessment.Features.Grading.GetTeacherGradebook;

internal static class GetTeacherGradebookEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/gradebook", GetTeacherGradebookAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageGradebook);
    }


    private static Task<IResult> GetTeacherGradebookAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, IIdentityDirectory identity, CancellationToken cancellationToken)
    {
        return GetGradebookAsync(classId, null, context, session, academics, identity, cancellationToken);
    }

}
