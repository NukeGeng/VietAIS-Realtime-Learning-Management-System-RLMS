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

public sealed record StudentLessonResourceResponse(Guid Id, string Title, string Description,
    LearningResourceType Type, string? ExternalUrl, string? FileName, string? ContentType,
    long Size, int? DurationSeconds, string? AccessUrl)
{
    public static StudentLessonResourceResponse From(LearningResource resource,
        string? accessUrl = null) =>
        new(resource.Id, resource.Title, resource.Description, resource.Type, resource.ExternalUrl,
            resource.FileName, resource.ContentType, resource.Size, resource.DurationSeconds,
            accessUrl ?? resource.ExternalUrl);
}
