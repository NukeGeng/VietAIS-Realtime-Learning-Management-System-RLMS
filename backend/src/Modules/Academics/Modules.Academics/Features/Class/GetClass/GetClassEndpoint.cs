using LearningPlatform.Academics.Api;
using JasperFx;
using LearningPlatform.Academics.Contracts;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Academics.Api.ClassEndpoints;

namespace LearningPlatform.Academics.Features.Class.GetClass;

internal static class GetClassEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/classes/{classId:guid}", GetClassAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
    }


    private static async Task<IResult> GetClassAsync(
        Guid classId, HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (!await CanReadClassAsync(context, session, learningClass, cancellationToken))
        {
            return Results.Forbid();
        }

        var teachers = await session.Query<ClassTeacherAssignment>()
            .Where(assignment => assignment.OrganizationId == learningClass.OrganizationId &&
                assignment.LearningClassId == classId &&
                assignment.Status == TeacherAssignmentStatus.Active)
            .OrderBy(assignment => assignment.AssignedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(ClassDetailResponse.From(learningClass, teachers));
    }

}
