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

namespace LearningPlatform.Learning.Features.Learning.UploadResource;

internal static class UploadResourceEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/learning/classes/{classId:guid}/resources/upload", UploadResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
    }


    private static async Task<IResult> UploadResourceAsync(Guid classId, HttpRequest request,
        HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        ILearningResourceStorage storage, CancellationToken cancellationToken)
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

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "Resources cannot be added to a completed or archived class." });
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { error = "The resource upload could not be read." });
        }

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return Results.BadRequest(new { error = "A resource file is required." });
        }

        if (file.Length <= 0 || file.Length > 250 * 1024 * 1024)
        {
            return Results.BadRequest(new { error = "The resource file must be between 1 byte and 250 MB." });
        }

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Results.BadRequest(new { error = "The resource file needs a name." });
        }

        if (!Enum.TryParse<LearningResourceType>(form["type"].ToString(), true, out var type) ||
            !Enum.IsDefined(type))
        {
            return Results.BadRequest(new { error = "The resource type is invalid." });
        }

        var contentType = NormalizeContentType(file);
        if (!IsAllowedContentType(type, contentType))
        {
            return Results.BadRequest(new { error = "The file format is not allowed for this resource type." });
        }

        var lessonText = form["lessonId"].ToString().Trim();
        Guid? lessonId = null;
        if (lessonText.Length > 0)
        {
            if (!Guid.TryParse(lessonText, out var parsedLessonId))
            {
                return Results.BadRequest(new { error = "The lessonId form value is invalid." });
            }

            lessonId = parsedLessonId;
        }

        var liveSessionText = form["liveSessionId"].ToString().Trim();
        Guid? liveSessionId = null;
        if (liveSessionText.Length > 0)
        {
            if (!Guid.TryParse(liveSessionText, out var parsedSessionId))
            {
                return Results.BadRequest(new { error = "The liveSessionId form value is invalid." });
            }

            liveSessionId = parsedSessionId;
        }

        if (type == LearningResourceType.Recording && liveSessionId is null)
        {
            return Results.BadRequest(new { error = "A recording must identify its live session." });
        }

        int? durationSeconds;
        var durationText = form["durationSeconds"].ToString().Trim();
        if (durationText.Length == 0)
        {
            durationSeconds = null;
        }
        else if (!int.TryParse(durationText, out var parsedDuration))
        {
            return Results.BadRequest(new { error = "Resource duration must be a whole number of seconds." });
        }
        else
        {
            durationSeconds = parsedDuration;
        }

        var resource = new LearningResource
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor.OrganizationId,
            CourseId = learningClass.CourseId,
            LearningClassId = classId,
            LessonId = lessonId,
            LiveSessionId = liveSessionId,
            Title = form["title"].ToString().Trim(),
            Description = form["description"].ToString().Trim(),
            Type = type,
            FileName = fileName,
            ContentType = contentType,
            Size = file.Length,
            DurationSeconds = durationSeconds,
            Status = LearningResourceStatus.Draft,
            CreatedBy = actor.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var storageKey = $"{actor.OrganizationId:D}/resources/{classId:D}/{resource.Id:D}/{resource.FileName}";
            resource.StorageKey = storageKey;
            ValidateResourceFields(resource);
            await ValidateReferencesAsync(resource, session, cancellationToken);
            await using var input = file.OpenReadStream();
            await storage.PutAsync(actor.OrganizationId, storageKey, input, contentType,
                cancellationToken);
            resource.StorageKey = storageKey;
            session.Insert(resource);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/learning/resources/{resource.Id:D}",
                LearningResourceResponse.From(resource));
        }
        catch (LearningResourceValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

}
