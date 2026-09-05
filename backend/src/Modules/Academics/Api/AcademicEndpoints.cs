using JasperFx;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LearningPlatform.Academics.Api;

public static class AcademicEndpoints
{
    public static void MapAcademicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/academics/courses", ListCoursesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadCourses);
        endpoints.MapGet("/api/academics/courses/{courseId:guid}", GetCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadCourses);
        endpoints.MapPost("/api/academics/courses", CreateCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
        endpoints.MapPut("/api/academics/courses/{courseId:guid}", UpdateCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/publish", PublishCourseAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules", CreateModuleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
        endpoints.MapPost("/api/academics/courses/{courseId:guid}/modules/{moduleId:guid}/lessons",
                CreateLessonAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageCourses);
    }

    private static async Task<IResult> ListCoursesAsync(
        int? page, int? pageSize, IQuerySession session, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page ?? 1, 1);
        var safePageSize = Math.Clamp(pageSize ?? 50, 1, 100);
        var courses = await session.Query<Course>()
            .OrderBy(course => course.Title)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);
        return Results.Ok(courses.Select(CourseResponse.From));
    }

    private static async Task<IResult> GetCourseAsync(
        Guid courseId, IQuerySession session, CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
        {
            return Results.NotFound();
        }

        var modules = await session.Query<CourseModule>()
            .Where(module => module.CourseId == courseId)
            .OrderBy(module => module.Order)
            .ToListAsync(cancellationToken);
        var lessons = await session.Query<Lesson>()
            .Where(lesson => lesson.CourseId == courseId)
            .OrderBy(lesson => lesson.Order)
            .ToListAsync(cancellationToken);
        return Results.Ok(CourseDetailResponse.From(course, modules, lessons));
    }

    private static async Task<IResult> CreateCourseAsync(
        CreateCourseRequest request,
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
            var code = AcademicRules.NormalizeCourseCode(request.Code);
            var title = AcademicRules.RequireTitle(request.Title);
            var description = AcademicRules.RequireDescription(request.Description);
            var now = DateTimeOffset.UtcNow;
            var course = new Course
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                Code = code,
                Title = title,
                Description = description,
                Status = CourseStatus.Draft,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(course);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/courses/{course.Id:D}", CourseResponse.From(course));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A course with that code already exists." });
        }
    }

    private static async Task<IResult> UpdateCourseAsync(
        Guid courseId,
        UpdateCourseRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
        {
            return Results.NotFound();
        }

        try
        {
            course.Code = AcademicRules.NormalizeCourseCode(request.Code);
            course.Title = AcademicRules.RequireTitle(request.Title);
            course.Description = AcademicRules.RequireDescription(request.Description);
            course.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(course);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(CourseResponse.From(course));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A course with that code already exists." });
        }
    }

    private static async Task<IResult> PublishCourseAsync(
        Guid courseId, IDocumentSession session, CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
        {
            return Results.NotFound();
        }

        if (course.Status == CourseStatus.Archived)
        {
            return Results.Conflict(new { error = "An archived course cannot be published." });
        }

        course.Status = CourseStatus.Published;
        course.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(course);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(CourseResponse.From(course));
    }

    private static async Task<IResult> CreateModuleAsync(
        Guid courseId,
        CreateModuleRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        if (course is null)
        {
            return Results.NotFound();
        }

        try
        {
            var title = AcademicRules.RequireTitle(request.Title);
            var order = AcademicRules.RequireOrder(request.Order);
            var existing = await session.Query<CourseModule>()
                .AnyAsync(module => module.CourseId == courseId && module.Order == order, cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { error = "A module with that order already exists." });
            }

            var module = new CourseModule
            {
                Id = Guid.NewGuid(),
                OrganizationId = course.OrganizationId,
                CourseId = courseId,
                Title = title,
                Order = order,
                Status = AcademicContentStatus.Draft
            };
            session.Insert(module);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/courses/{courseId:D}/modules/{module.Id:D}",
                ModuleResponse.From(module));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> CreateLessonAsync(
        Guid courseId,
        Guid moduleId,
        CreateLessonRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var course = await session.LoadAsync<Course>(courseId, cancellationToken);
        var module = await session.LoadAsync<CourseModule>(moduleId, cancellationToken);
        if (course is null || module is null || module.CourseId != courseId)
        {
            return Results.NotFound();
        }

        try
        {
            var title = AcademicRules.RequireTitle(request.Title);
            var summary = AcademicRules.RequireContentSummary(request.ContentSummary);
            var order = AcademicRules.RequireOrder(request.Order);
            var existing = await session.Query<Lesson>()
                .AnyAsync(lesson => lesson.CourseModuleId == moduleId && lesson.Order == order,
                    cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { error = "A lesson with that order already exists." });
            }

            var lesson = new Lesson
            {
                Id = Guid.NewGuid(),
                OrganizationId = course.OrganizationId,
                CourseId = courseId,
                CourseModuleId = moduleId,
                Title = title,
                Order = order,
                ContentSummary = summary,
                Status = AcademicContentStatus.Draft
            };
            session.Insert(lesson);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/academics/courses/{courseId:D}/modules/{moduleId:D}/lessons/{lesson.Id:D}",
                LessonResponse.From(lesson));
        }
        catch (AcademicValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}

public sealed record CreateCourseRequest(string Code, string Title, string? Description);
public sealed record UpdateCourseRequest(string Code, string Title, string? Description);
public sealed record CreateModuleRequest(string Title, int Order);
public sealed record CreateLessonRequest(string Title, int Order, string? ContentSummary);

public sealed record CourseResponse(
    Guid Id, string Code, string Title, string Description, CourseStatus Status,
    Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static CourseResponse From(Course course) => new(course.Id, course.Code, course.Title,
        course.Description, course.Status, course.CreatedBy, course.CreatedAt, course.UpdatedAt);
}

public sealed record ModuleResponse(Guid Id, Guid CourseId, string Title, int Order,
    AcademicContentStatus Status)
{
    public static ModuleResponse From(CourseModule module) =>
        new(module.Id, module.CourseId, module.Title, module.Order, module.Status);
}

public sealed record LessonResponse(Guid Id, Guid CourseId, Guid CourseModuleId, string Title,
    int Order, string ContentSummary, AcademicContentStatus Status)
{
    public static LessonResponse From(Lesson lesson) => new(lesson.Id, lesson.CourseId,
        lesson.CourseModuleId, lesson.Title, lesson.Order, lesson.ContentSummary, lesson.Status);
}

public sealed record CourseDetailResponse(CourseResponse Course, IReadOnlyList<ModuleResponse> Modules,
    IReadOnlyList<LessonResponse> Lessons)
{
    public static CourseDetailResponse From(Course course, IEnumerable<CourseModule> modules,
        IEnumerable<Lesson> lessons) => new(CourseResponse.From(course),
            modules.Select(ModuleResponse.From).ToArray(), lessons.Select(LessonResponse.From).ToArray());
}
