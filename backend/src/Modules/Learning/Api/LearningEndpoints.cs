using LearningPlatform.Academics.Authorization;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LearningPlatform.Learning.Api;

public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/progress", GetClassProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapGet("/api/learning/classes/{classId:guid}/progress/me", GetMyProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }

    private static async Task<IResult> GetClassProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var canManage = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
        if (!canManage)
        {
            return Results.Forbid();
        }

        var records = await session.Query<StudentProgressRecord>()
            .Where(record => record.LearningClassId == classId)
            .OrderBy(record => record.StudentId)
            .ThenBy(record => record.ActivityId)
            .ToListAsync(cancellationToken);
        return Results.Ok(new LearningProgressResponse(classId,
            records.Select(LearningProgressEntryResponse.From).ToArray()));
    }

    private static async Task<IResult> GetMyProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, classId, actor.UserId,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await session.Query<StudentProgressRecord>()
            .Where(record => record.LearningClassId == classId && record.StudentId == actor.UserId)
            .OrderBy(record => record.ActivityId)
            .ToListAsync(cancellationToken);
        return Results.Ok(new LearningProgressResponse(classId,
            records.Select(LearningProgressEntryResponse.From).ToArray()));
    }
}

public sealed record LearningProgressResponse(Guid LearningClassId,
    LearningProgressEntryResponse[] Entries);

public sealed record LearningProgressEntryResponse(Guid StudentId, Guid ActivityId,
    string ActivityTitle, decimal Score, decimal MaxScore, decimal Completion,
    LearningProgressStatus Status, DateTimeOffset? CompletedAt, int Version)
{
    public static LearningProgressEntryResponse From(StudentProgressRecord record) => new(record.StudentId,
        record.ActivityId, record.ActivityTitle, record.Score, record.MaxScore, record.Completion,
        record.Status, record.CompletedAt, record.Version);
}
