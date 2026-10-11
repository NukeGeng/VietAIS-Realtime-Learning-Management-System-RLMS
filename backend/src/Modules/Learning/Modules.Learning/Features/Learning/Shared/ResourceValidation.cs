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

internal static class ResourceValidation
{


    internal static async Task<IResult> ChangeResourceStatusAsync(Guid resourceId,
        LearningResourceStatus target, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var resource = await session.LoadAsync<LearningResource>(resourceId, cancellationToken);
        if (resource is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, resource.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId,
            resource.LearningClassId, cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot change resource status." });
        }

        var valid = (resource.Status, target) switch
        {
            (LearningResourceStatus.Draft, LearningResourceStatus.Published) => true,
            (LearningResourceStatus.Published, LearningResourceStatus.Archived) => true,
            _ => false
        };
        if (!valid)
        {
            return Results.Conflict(new { error = "The resource cannot make that state transition." });
        }

        resource.Status = target;
        resource.PublishedAt = target == LearningResourceStatus.Published
            ? DateTimeOffset.UtcNow
            : resource.PublishedAt;
        resource.UpdatedAt = DateTimeOffset.UtcNow;
        session.Store(resource);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(LearningResourceResponse.From(resource));
    }



    internal static async Task<LearningResource> BuildResourceAsync(Guid? lessonId,
        Guid? liveSessionId, string? title, string? description, LearningResourceType type,
        string? externalUrl, int? durationSeconds, Guid classId, Guid courseId,
        AuthenticatedActor actor, IQuerySession session, CancellationToken cancellationToken)
    {
        var resource = new LearningResource
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            CourseId = courseId,
            LearningClassId = classId,
            LessonId = lessonId,
            LiveSessionId = liveSessionId,
            Title = title?.Trim() ?? "",
            Description = description?.Trim() ?? "",
            Type = type,
            ExternalUrl = externalUrl?.Trim(),
            DurationSeconds = durationSeconds,
            Status = LearningResourceStatus.Draft,
            CreatedBy = actor.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        ValidateResourceFields(resource);
        await ValidateReferencesAsync(resource, session, cancellationToken);
        return resource;
    }



    internal static async Task ValidateReferencesAsync(LearningResource resource,
        IQuerySession session, CancellationToken cancellationToken)
    {
        if (resource.LessonId is { } lessonId)
        {
            var lesson = await session.LoadAsync<Lesson>(lessonId, cancellationToken);
            if (lesson is null || lesson.CourseId != resource.CourseId)
            {
                throw new LearningResourceValidationException("The lesson does not belong to this course.");
            }
        }

        if (resource.LiveSessionId is { } liveSessionId)
        {
            var liveSession = await session.LoadAsync<LiveSession>(liveSessionId, cancellationToken);
            if (liveSession is null || liveSession.LearningClassId != resource.LearningClassId ||
                liveSession.Status != LiveSessionStatus.Ended)
            {
                throw new LearningResourceValidationException("The recording session is not an ended session of this class.");
            }
        }
    }



    internal static void ValidateResourceFields(LearningResource resource)
    {
        if (!Enum.IsDefined(resource.Type) || string.IsNullOrWhiteSpace(resource.Title) ||
            resource.Title.Length > 200 || resource.Description.Length > 20_000)
        {
            throw new LearningResourceValidationException("A resource needs a title within 200 characters and a description within 20000 characters.");
        }

        if (resource.DurationSeconds is < 0 or > 86_400)
        {
            throw new LearningResourceValidationException("Resource duration must be between zero and 86400 seconds.");
        }

        if (resource.Type is LearningResourceType.Video or LearningResourceType.Recording &&
            string.IsNullOrWhiteSpace(resource.ExternalUrl) && string.IsNullOrWhiteSpace(resource.StorageKey))
        {
            throw new LearningResourceValidationException("A video or recording needs a URL or uploaded file.");
        }

        if (resource.Type == LearningResourceType.Recording && resource.LiveSessionId is null)
        {
            throw new LearningResourceValidationException("A recording must identify its live session.");
        }

        if (resource.Type is not (LearningResourceType.Video or LearningResourceType.Recording) &&
            string.IsNullOrWhiteSpace(resource.ExternalUrl) && string.IsNullOrWhiteSpace(resource.StorageKey))
        {
            throw new LearningResourceValidationException("A resource needs an external URL or uploaded file.");
        }

        if (!string.IsNullOrWhiteSpace(resource.ExternalUrl) &&
            (!Uri.TryCreate(resource.ExternalUrl, UriKind.Absolute, out var uri) ||
             uri.Scheme is not ("http" or "https")))
        {
            throw new LearningResourceValidationException("Resource URLs must use http or https.");
        }
    }



    internal static bool IsAllowedContentType(LearningResourceType type, string contentType) => type switch
    {
        LearningResourceType.Document => contentType is "application/pdf" or
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        LearningResourceType.Slide => contentType is "application/pdf" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" or
            "application/vnd.ms-powerpoint",
        LearningResourceType.Video or LearningResourceType.Recording => contentType.StartsWith("video/",
            StringComparison.OrdinalIgnoreCase),
        _ => !string.IsNullOrWhiteSpace(contentType)
    };



    internal static string NormalizeContentType(IFormFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType.Trim().ToLowerInvariant();
        }

        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            _ => (file.ContentType ?? "").Trim().ToLowerInvariant()
        };
    }

}
