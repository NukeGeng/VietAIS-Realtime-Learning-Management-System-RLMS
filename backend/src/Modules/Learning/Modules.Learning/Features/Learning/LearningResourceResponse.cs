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

public sealed record LearningResourceResponse(Guid Id, Guid CourseId, Guid LearningClassId,
    Guid? LessonId, Guid? LiveSessionId, string Title, string Description, LearningResourceType Type,
    string? ExternalUrl, string? FileName, string? ContentType, long Size, int? DurationSeconds,
    LearningResourceStatus Status, DateTimeOffset? PublishedAt, string? AccessUrl)
{
    public static LearningResourceResponse From(LearningResource resource, string? accessUrl = null) =>
        new(resource.Id, resource.CourseId, resource.LearningClassId, resource.LessonId,
            resource.LiveSessionId, resource.Title, resource.Description, resource.Type,
            resource.ExternalUrl, resource.FileName, resource.ContentType, resource.Size,
            resource.DurationSeconds, resource.Status, resource.PublishedAt,
            accessUrl ?? resource.ExternalUrl);
}
