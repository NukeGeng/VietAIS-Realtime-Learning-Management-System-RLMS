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

public sealed record ClassResponse(Guid Id, Guid CourseId, string Code, string Name,
    LearningClassStatus Status, DateOnly StartDate, DateOnly EndDate)
{
    public string? CourseName { get; init; }
    public int? StudentCount { get; init; }
    public static ClassResponse From(LearningClass learningClass) => new(learningClass.Id,
        learningClass.CourseId, learningClass.Code, learningClass.Name, learningClass.Status,
        learningClass.StartDate, learningClass.EndDate);
}
