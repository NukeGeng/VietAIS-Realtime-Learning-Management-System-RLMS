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

namespace LearningPlatform.Learning.Api;

public sealed record LessonProgressEntryResponse(Guid StudentId, Guid LessonId, string LessonTitle,
    LessonProgressStatus? Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset? UpdatedAt)
{
    public static LessonProgressEntryResponse From(Guid studentId, Lesson lesson,
        StudentLessonProgressRecord? record) => new(studentId, lesson.Id, lesson.Title,
        record?.Status, record?.StartedAt, record?.CompletedAt, record?.UpdatedAt);
}
