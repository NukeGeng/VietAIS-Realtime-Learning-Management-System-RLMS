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

namespace LearningPlatform.Learning.Features.Learning.Shared;

internal static class CourseProgress
{


    internal static async Task<IResult> GetCourseProgressCoreAsync(Guid classId, Guid? studentFilter,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var isTeacher = studentFilter is null;
        if (isTeacher)
        {
            if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
            {
                return Results.Forbid();
            }
        }
        else if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                     actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var lessons = await session.Query<Lesson>()
            .Where(lesson => lesson.OrganizationId == actor.OrganizationId &&
                lesson.CourseId == learningClass.CourseId &&
                lesson.Status == AcademicContentStatus.Published)
            .OrderBy(lesson => lesson.Order)
            .ToListAsync(cancellationToken);
        var studentIds = isTeacher
            ? await session.Query<Enrollment>()
                .Where(enrollment => enrollment.OrganizationId == actor.OrganizationId &&
                    enrollment.LearningClassId == classId &&
                    enrollment.Status == EnrollmentStatus.Active)
                .Select(enrollment => enrollment.StudentId)
                .OrderBy(studentId => studentId)
                .ToListAsync(cancellationToken)
            : [actor.UserId];
        if (studentIds.Count == 0)
        {
            return Results.Ok(new CourseProgressResponse(classId, learningClass.CourseId,
                lessons.Count, []));
        }

        var records = await session.Query<StudentLessonProgressRecord>()
            .Where(record => record.OrganizationId == actor.OrganizationId &&
                record.LearningClassId == classId &&
                studentIds.Contains(record.StudentId))
            .ToListAsync(cancellationToken);
        var entries = studentIds.SelectMany(studentId => lessons.Select(lesson =>
        {
            var record = records.FirstOrDefault(value => value.StudentId == studentId &&
                value.LessonId == lesson.Id);
            return LessonProgressEntryResponse.From(studentId, lesson, record);
        })).ToArray();
        return Results.Ok(new CourseProgressResponse(classId, learningClass.CourseId,
            lessons.Count, entries));
    }

}
