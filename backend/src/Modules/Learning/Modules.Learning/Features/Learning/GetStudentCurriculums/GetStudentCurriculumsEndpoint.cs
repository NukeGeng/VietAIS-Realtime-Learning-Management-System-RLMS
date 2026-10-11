using LearningPlatform.Learning.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Learning.Storage;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Learning.Features.Learning.Shared.CurriculumContent;
using static LearningPlatform.Learning.Features.Learning.Shared.CourseProgress;
using static LearningPlatform.Learning.Features.Learning.Shared.ResourceValidation;
using static LearningPlatform.Learning.Features.Learning.Shared.LearningAccess;

namespace LearningPlatform.Learning.Features.Learning.GetStudentCurriculums;

internal static class GetStudentCurriculumsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/curriculums",
                GetStudentCurriculumsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }


    private static async Task<IResult> GetStudentCurriculumsAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
            return Results.Forbid();

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
            return Results.NotFound();
        if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken))
            return Results.Forbid();

        var course = await session.LoadAsync<Course>(learningClass.CourseId, cancellationToken);
        if (course is null || course.OrganizationId != actor.OrganizationId ||
            course.Status != CourseStatus.Published)
            return Results.NotFound();

        var assignments = await session.Query<ClassCurriculumAssignment>()
            .Where(assignment => assignment.OrganizationId == actor.OrganizationId &&
                assignment.LearningClassId == classId &&
                assignment.Status == ClassCurriculumAssignmentStatus.Active)
            .OrderBy(assignment => assignment.Order)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        var cards = new List<StudentCurriculumSummaryResponse>();
        foreach (var assignment in assignments)
        {
            var resolution = await ResolveStudentCurriculumAsync(classId, assignment.CurriculumId,
                actor, session, academics, cancellationToken);
            if (resolution is null)
                continue;
            var snapshot = await BuildStudentCurriculumContentAsync(resolution, actor.UserId,
                session, cancellationToken);
            cards.Add(StudentCurriculumSummaryResponse.From(snapshot));
        }

        // Compatibility for demo/legacy data where modules still belong directly to Course.
        // This keeps existing classes usable while the durable assignment model is introduced.
        if (assignments.Count == 0)
        {
            var resolution = new StudentCurriculumResolution(learningClass,
                course, new Curriculum
                {
                    Id = course.Id,
                    OrganizationId = course.OrganizationId,
                    CourseId = course.Id,
                    Title = course.Title,
                    Description = course.Description,
                    Status = AcademicContentStatus.Published
                }, true);
            var snapshot = await BuildStudentCurriculumContentAsync(resolution, actor.UserId,
                session, cancellationToken);
            cards.Add(StudentCurriculumSummaryResponse.From(snapshot));
        }

        return Results.Ok(new StudentCurriculumListResponse(classId, cards.ToArray()));
    }

}
