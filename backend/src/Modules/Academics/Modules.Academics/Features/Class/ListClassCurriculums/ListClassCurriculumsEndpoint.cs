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

namespace LearningPlatform.Academics.Features.Class.ListClassCurriculums;

internal static class ListClassCurriculumsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/classes/{classId:guid}/curriculums",
                ListClassCurriculumsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
    }


    private static async Task<IResult> ListClassCurriculumsAsync(
        Guid classId, HttpContext context, IQuerySession session,
        CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
            return Results.NotFound();
        if (!await CanReadClassAsync(context, session, learningClass, cancellationToken))
            return Results.Forbid();

        var assignments = await session.Query<ClassCurriculumAssignment>()
            .Where(value => value.OrganizationId == learningClass.OrganizationId &&
                value.LearningClassId == classId &&
                value.Status == ClassCurriculumAssignmentStatus.Active)
            .OrderBy(value => value.Order)
            .ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        var curriculumIds = assignments.Select(value => value.CurriculumId).ToArray();
        var curriculums = curriculumIds.Length == 0
            ? []
            : await session.Query<Curriculum>()
                .Where(value => curriculumIds.Contains(value.Id))
                .ToListAsync(cancellationToken);
        var byId = curriculums.ToDictionary(value => value.Id);
        return Results.Ok(assignments
            .Where(value => byId.ContainsKey(value.CurriculumId))
            .Select(value => ClassCurriculumAssignmentResponse.From(value,
                byId[value.CurriculumId]))
            .ToArray());
    }

}
