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

namespace LearningPlatform.Learning.Features.Learning.UpdateLessonProgress;

internal static class UpdateLessonProgressEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/learning/classes/{classId:guid}/lessons/{lessonId:guid}/progress",
                UpdateLessonProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
    }


    private static async Task<IResult> UpdateLessonProgressAsync(Guid classId, Guid lessonId,
        UpdateLessonProgressRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (request.Status is not (LessonProgressStatus.InProgress or LessonProgressStatus.Completed))
        {
            return Results.BadRequest(new { error = "Lesson progress status is invalid." });
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot receive lesson progress." });
        }

        if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var lesson = await session.LoadAsync<Lesson>(lessonId, cancellationToken);
        if (lesson is null || lesson.CourseId != learningClass.CourseId ||
            lesson.Status != AcademicContentStatus.Published)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var id = LearningConstants.LessonProgressId(lessonId, classId, actor.UserId);
        var record = await session.LoadAsync<StudentLessonProgressRecord>(id, cancellationToken);
        if (record is not null && record.Status == LessonProgressStatus.Completed &&
            request.Status == LessonProgressStatus.InProgress)
        {
            return Results.Conflict(new { error = "Completed lesson progress cannot move backwards." });
        }

        var isNew = record is null;
        record ??= new StudentLessonProgressRecord
        {
            Id = id,
            OrganizationId = actor.OrganizationId,
            LearningClassId = classId,
            CourseId = learningClass.CourseId,
            LessonId = lessonId,
            StudentId = actor.UserId,
            StartedAt = now
        };
        record.Status = request.Status;
        record.CompletedAt = request.Status == LessonProgressStatus.Completed
            ? record.CompletedAt ?? now
            : null;
        record.UpdatedAt = now;
        if (isNew)
        {
            session.Insert(record);
        }
        else
        {
            session.Update(record);
        }

        await session.SaveChangesAsync(cancellationToken);
        try
        {
            await messageBus.PublishAsync(new LessonProgressChangedForReporting(
                actor.OrganizationId, classId, actor.UserId));
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LearningPlatform.Learning.Progress").LogWarning(exception,
                "Unable to enqueue lesson progress reporting refresh for {ClassId}/{StudentId}.",
                classId, actor.UserId);
        }

        return Results.Ok(LessonProgressEntryResponse.From(actor.UserId, lesson, record));
    }

}
