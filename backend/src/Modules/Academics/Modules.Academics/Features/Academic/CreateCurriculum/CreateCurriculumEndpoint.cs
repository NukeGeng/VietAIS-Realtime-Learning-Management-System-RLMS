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
using static LearningPlatform.Academics.Api.AcademicEndpoints;

namespace LearningPlatform.Academics.Features.Academic.CreateCurriculum;

internal static class CreateCurriculumEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/curriculums",
                CreateCurriculumAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }


    private static async Task<IResult> CreateCurriculumAsync(
        Guid courseId, CreateCurriculumRequest request, HttpContext context,
        IDocumentSession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
            return Results.Forbid();

        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null || course.OrganizationId != actor!.OrganizationId)
            return Results.NotFound();
        if (!AcademicAuthorization.CanManageCourse(actor, course))
            return Results.Forbid();
        if (course.Status == CourseStatus.Archived)
            return Results.Conflict(new { error = "An archived course cannot receive a curriculum." });

        try
        {
            var title = AcademicRules.RequireTitle(request.Title);
            var description = AcademicRules.RequireDescription(request.Description);
            var existing = await session.Query<Curriculum>().AnyAsync(value =>
                value.OrganizationId == actor.OrganizationId && value.CourseId == courseId &&
                value.Title == title && value.Status != AcademicContentStatus.Archived,
                cancellationToken);
            if (existing)
                return Results.Conflict(new { error = "A curriculum with that title already exists." });

            var now = DateTimeOffset.UtcNow;
            var curriculum = new Curriculum
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                CourseId = courseId,
                Title = title,
                Description = description,
                Status = AcademicContentStatus.Draft,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(curriculum);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/curriculums/{curriculum.Id:D}",
                CurriculumResponse.From(curriculum));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
