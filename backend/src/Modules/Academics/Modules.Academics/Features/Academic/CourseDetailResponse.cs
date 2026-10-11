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

public sealed record CourseDetailResponse(CourseResponse Course, IReadOnlyList<ModuleResponse> Modules,
    IReadOnlyList<LessonResponse> Lessons)
{
    public static CourseDetailResponse From(Course course, IEnumerable<CourseModule> modules,
        IEnumerable<Lesson> lessons) => new(CourseResponse.From(course),
            modules.Select(ModuleResponse.From).ToArray(), lessons.Select(LessonResponse.From).ToArray());
}
