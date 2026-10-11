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

namespace LearningPlatform.Academics.Features.Class.CreateClass;

internal static class CreateClassEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/academics/classes", CreateClassAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageClasses);
    }


    private static async Task<IResult> CreateClassAsync(
        CreateClassRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var course = await session.LoadAsync<Course>(request.CourseId, cancellationToken);
            if (course is null)
            {
                return Results.BadRequest(new { error = "The course does not exist." });
            }

            if (course.Status != CourseStatus.Published)
            {
                return Results.Conflict(new { error = "A class can only use a published course." });
            }

            var code = AcademicRules.NormalizeCourseCode(request.Code);
            var name = AcademicRules.RequireTitle(request.Name);
            var dates = AcademicRules.RequireDateRange(request.StartDate, request.EndDate);
            var now = DateTimeOffset.UtcNow;
            var learningClass = new LearningClass
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                CourseId = request.CourseId,
                Code = code,
                Name = name,
                CreatedBy = actor.UserId,
                Status = LearningClassStatus.Preparing,
                StartDate = dates.Start,
                EndDate = dates.End,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(learningClass);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/classes/{learningClass.Id:D}",
                ClassResponse.From(learningClass));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A class with that code already exists." });
        }
    }

}
