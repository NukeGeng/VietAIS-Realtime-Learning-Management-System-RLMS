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

namespace LearningPlatform.Academics.Api;

public sealed record LessonResponse(Guid Id, Guid CourseId, Guid CourseModuleId, string Title,
    int Order, string ContentSummary, AcademicContentStatus Status)
{
    public static LessonResponse From(Lesson lesson) => new(lesson.Id, lesson.CourseId,
        lesson.CourseModuleId, lesson.Title, lesson.Order, lesson.ContentSummary, lesson.Status);
}
