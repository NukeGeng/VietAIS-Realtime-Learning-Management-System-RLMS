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

internal static class CurriculumContent
{

    internal static async Task<StudentCurriculumResolution?> ResolveStudentCurriculumAsync(
        Guid classId, Guid curriculumId, AuthenticatedActor actor, IQuerySession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        var learningClass = await academics.FindClassAsync(actor.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
            return null;
        var course = await session.LoadAsync<Course>(learningClass.CourseId, cancellationToken);
        if (course is null || course.OrganizationId != actor.OrganizationId ||
            course.Status != CourseStatus.Published)
            return null;

        var hasAssignments = await session.Query<ClassCurriculumAssignment>()
            .AnyAsync(assignment => assignment.OrganizationId == actor.OrganizationId &&
                assignment.LearningClassId == classId &&
                assignment.Status == ClassCurriculumAssignmentStatus.Active);
        var curriculum = await session.LoadAsync<Curriculum>(curriculumId, cancellationToken);
        if (curriculum is not null && curriculum.OrganizationId == actor.OrganizationId &&
            curriculum.CourseId == course.Id && curriculum.Status == AcademicContentStatus.Published &&
            await session.Query<ClassCurriculumAssignment>().AnyAsync(assignment =>
                assignment.OrganizationId == actor.OrganizationId &&
                assignment.LearningClassId == classId && assignment.CurriculumId == curriculumId &&
                assignment.Status == ClassCurriculumAssignmentStatus.Active))
        {
            return new StudentCurriculumResolution(learningClass, course, curriculum, false);
        }

        return !hasAssignments && curriculumId == course.Id
            ? new StudentCurriculumResolution(learningClass, course, new Curriculum
            {
                Id = course.Id,
                OrganizationId = course.OrganizationId,
                CourseId = course.Id,
                Title = course.Title,
                Description = course.Description,
                Status = AcademicContentStatus.Published
            }, true)
            : null;
    }



    internal static async Task<StudentCurriculumContentResponse> BuildStudentCurriculumContentAsync(
        StudentCurriculumResolution resolution, Guid studentId, IQuerySession session,
        CancellationToken cancellationToken)
    {
        var modules = await session.Query<CourseModule>()
            .Where(module => module.CourseId == resolution.Course.Id &&
                module.OrganizationId == resolution.Course.OrganizationId &&
                module.Status == AcademicContentStatus.Published)
            .OrderBy(module => module.Order)
            .ThenBy(module => module.Id)
            .ToListAsync(cancellationToken);
        var moduleIds = modules.Select(module => module.Id).ToArray();
        var lessons = moduleIds.Length == 0
            ? new List<Lesson>()
            : await session.Query<Lesson>()
                .Where(lesson => lesson.CourseId == resolution.Course.Id &&
                    lesson.OrganizationId == resolution.Course.OrganizationId &&
                    lesson.Status == AcademicContentStatus.Published &&
                    moduleIds.Contains(lesson.CourseModuleId))
                .OrderBy(lesson => lesson.CourseModuleId)
                .ThenBy(lesson => lesson.Order)
                .ThenBy(lesson => lesson.Id)
                .ToListAsync(cancellationToken);
        var lessonIds = lessons.Select(lesson => lesson.Id).ToArray();
        var records = lessonIds.Length == 0
            ? new List<StudentLessonProgressRecord>()
            : await session.Query<StudentLessonProgressRecord>()
                .Where(record => record.OrganizationId == resolution.Course.OrganizationId &&
                    record.LearningClassId == resolution.LearningClass.Id &&
                    record.StudentId == studentId && lessonIds.Contains(record.LessonId) &&
                    (record.CurriculumId == resolution.Curriculum.Id ||
                     (record.CurriculumId == null && record.CourseId == resolution.Course.Id)))
                .ToListAsync(cancellationToken);
        var moduleViews = modules.Select(module =>
        {
            var moduleLessons = lessons.Where(lesson => lesson.CourseModuleId == module.Id)
                .Select(lesson =>
                {
                    var progress = records.FirstOrDefault(record => record.LessonId == lesson.Id);
                    return new StudentLessonSummaryResponse(lesson.Id, lesson.Title, lesson.Order,
                        progress?.Status);
                }).ToArray();
            return new StudentCourseModuleResponse(module.Id, module.Title, module.Order,
                moduleLessons.Count(lesson => lesson.Status == LessonProgressStatus.Completed),
                moduleLessons.Length, moduleLessons);
        }).ToArray();
        var completedLessons = moduleViews.Sum(module => module.CompletedLessons);
        var title = resolution.IsLegacy && modules.Count == 1 && modules[0].Title.Length > 0
            ? modules[0].Title
            : resolution.Curriculum.Title;
        return new StudentCurriculumContentResponse(resolution.LearningClass.Id,
            resolution.Curriculum.Id, resolution.Course.Id, title,
            resolution.Curriculum.Description, resolution.Course.Title,
            completedLessons, lessons.Count,
            lessons.Count == 0 ? 0m : decimal.Round(completedLessons * 100m / lessons.Count, 2),
            moduleViews);
    }



    internal static async Task<StudentLessonProgressRecord?> LoadCurriculumProgressAsync(
        Guid curriculumId, Guid lessonId, Guid classId, Guid studentId, IQuerySession session,
        CancellationToken cancellationToken)
    {
        var record = await session.LoadAsync<StudentLessonProgressRecord>(
            LearningConstants.CurriculumLessonProgressId(curriculumId, lessonId, classId, studentId),
            cancellationToken);
        return record ?? (curriculumId == Guid.Empty ? null : await session.LoadAsync<StudentLessonProgressRecord>(
            LearningConstants.LessonProgressId(lessonId, classId, studentId), cancellationToken));
    }

}
