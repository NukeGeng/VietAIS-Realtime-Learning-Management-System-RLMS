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

public sealed record StudentCurriculumSummaryResponse(Guid CurriculumId, Guid CourseId,
    string Title, string Description, int ModuleCount, int LessonCount, int CompletedLessons,
    decimal Progress)
{
    public static StudentCurriculumSummaryResponse From(StudentCurriculumContentResponse snapshot) =>
        new(snapshot.CurriculumId, snapshot.CourseId, snapshot.CurriculumTitle,
            snapshot.CurriculumDescription, snapshot.Modules.Length, snapshot.TotalLessons,
            snapshot.CompletedLessons, snapshot.Progress);
}
