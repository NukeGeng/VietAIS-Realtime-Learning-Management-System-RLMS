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

namespace LearningPlatform.Academics.Features.Class.AssignCurriculum;

internal static class AssignCurriculumEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes/{classId:guid}/curriculums",
                AssignCurriculumAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
    }


    private static async Task<IResult> AssignCurriculumAsync(
        Guid classId, AssignCurriculumRequest request, HttpContext context,
        IDocumentSession session, CancellationToken cancellationToken)
    {
        var learningClass = await session.LoadAsync<LearningClass>(classId, cancellationToken);
        if (learningClass is null)
            return Results.NotFound();
        if (!await CanManageClassAsync(context, session, learningClass, cancellationToken))
            return Results.Forbid();
        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
            return Results.Conflict(new { error = "A completed or archived class cannot change curriculums." });

        var curriculum = await session.LoadAsync<Curriculum>(request.CurriculumId,
            cancellationToken);
        if (curriculum is null || curriculum.OrganizationId != learningClass.OrganizationId ||
            curriculum.CourseId != learningClass.CourseId)
            return Results.BadRequest(new { error = "The curriculum does not belong to this class course." });
        if (curriculum.Status != AcademicContentStatus.Published)
            return Results.Conflict(new { error = "Only a published curriculum can be assigned." });

        var existing = await session.Query<ClassCurriculumAssignment>()
            .FirstOrDefaultAsync(value => value.LearningClassId == classId &&
                value.CurriculumId == request.CurriculumId, cancellationToken);
        var order = request.Order < 0 ? 0 : request.Order;
        if (existing is not null && existing.Status == ClassCurriculumAssignmentStatus.Active)
            return Results.Conflict(new { error = "The curriculum is already assigned to this class." });

        if (existing is null)
        {
            existing = new ClassCurriculumAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = learningClass.OrganizationId,
                LearningClassId = classId,
                CurriculumId = curriculum.Id,
                Order = order,
                Status = ClassCurriculumAssignmentStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            session.Insert(existing);
        }
        else
        {
            existing.Order = order;
            existing.Status = ClassCurriculumAssignmentStatus.Active;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(existing);
        }

        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/academics/classes/{classId:D}/curriculums/{curriculum.Id:D}",
            ClassCurriculumAssignmentResponse.From(existing, curriculum));
    }

}
