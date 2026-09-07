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

public static class LearningEndpoints
{
    public static void MapLearningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/learning/classes/{classId:guid}/progress", GetClassProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapGet("/api/learning/classes/{classId:guid}/progress/me", GetMyProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapGet("/api/learning/classes/{classId:guid}/course-progress", GetCourseProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapGet("/api/learning/classes/{classId:guid}/course-progress/me",
                GetMyCourseProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapPost("/api/learning/classes/{classId:guid}/lessons/{lessonId:guid}/progress",
                UpdateLessonProgressAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningProgress);
        endpoints.MapGet("/api/learning/classes/{classId:guid}/resources", ListResourcesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningResources);
        endpoints.MapGet("/api/learning/resources/{resourceId:guid}", GetResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadLearningResources);
        endpoints.MapPost("/api/learning/classes/{classId:guid}/resources", CreateResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
        endpoints.MapPost("/api/learning/classes/{classId:guid}/resources/upload", UploadResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
        endpoints.MapPost("/api/learning/resources/{resourceId:guid}/publish", PublishResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
        endpoints.MapPost("/api/learning/resources/{resourceId:guid}/archive", ArchiveResourceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageLearningResources);
    }

    private static async Task<IResult> GetClassProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var canManage = actor.Permissions.Contains(PermissionCatalog.ManageOrganizations,
                StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
        if (!canManage)
        {
            return Results.Forbid();
        }

        var records = await session.Query<StudentProgressRecord>()
            .Where(record => record.LearningClassId == classId)
            .OrderBy(record => record.StudentId)
            .ThenBy(record => record.ActivityId)
            .ToListAsync(cancellationToken);
        return Results.Ok(new LearningProgressResponse(classId,
            records.Select(LearningProgressEntryResponse.From).ToArray()));
    }

    private static async Task<IResult> GetMyProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, classId, actor.UserId,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await session.Query<StudentProgressRecord>()
            .Where(record => record.LearningClassId == classId && record.StudentId == actor.UserId)
            .OrderBy(record => record.ActivityId)
            .ToListAsync(cancellationToken);
        records = records.Where(record => record.ResultsReleaseAt is null ||
                DateTimeOffset.UtcNow >= record.ResultsReleaseAt)
            .ToList();
        return Results.Ok(new LearningProgressResponse(classId,
            records.Select(LearningProgressEntryResponse.From).ToArray()));
    }

    private static Task<IResult> GetCourseProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        GetCourseProgressCoreAsync(classId, null, context, session, academics, cancellationToken);

    private static Task<IResult> GetMyCourseProgressAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        GetCourseProgressCoreAsync(classId, Guid.Empty, context, session, academics, cancellationToken);

    private static async Task<IResult> GetCourseProgressCoreAsync(Guid classId, Guid? studentFilter,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
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

        var isTeacher = studentFilter is null;
        if (isTeacher)
        {
            if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
            {
                return Results.Forbid();
            }
        }
        else if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                     actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var lessons = await session.Query<Lesson>()
            .Where(lesson => lesson.CourseId == learningClass.CourseId &&
                lesson.Status == AcademicContentStatus.Published)
            .OrderBy(lesson => lesson.Order)
            .ToListAsync(cancellationToken);
        var studentIds = isTeacher
            ? await session.Query<Enrollment>()
                .Where(enrollment => enrollment.LearningClassId == classId &&
                    enrollment.Status == EnrollmentStatus.Active)
                .Select(enrollment => enrollment.StudentId)
                .OrderBy(studentId => studentId)
                .ToListAsync(cancellationToken)
            : [actor.UserId];
        if (studentIds.Count == 0)
        {
            return Results.Ok(new CourseProgressResponse(classId, learningClass.CourseId,
                lessons.Count, []));
        }

        var records = await session.Query<StudentLessonProgressRecord>()
            .Where(record => record.LearningClassId == classId &&
                studentIds.Contains(record.StudentId))
            .ToListAsync(cancellationToken);
        var entries = studentIds.SelectMany(studentId => lessons.Select(lesson =>
        {
            var record = records.FirstOrDefault(value => value.StudentId == studentId &&
                value.LessonId == lesson.Id);
            return LessonProgressEntryResponse.From(studentId, lesson, record);
        })).ToArray();
        return Results.Ok(new CourseProgressResponse(classId, learningClass.CourseId,
            lessons.Count, entries));
    }

    private static async Task<IResult> UpdateLessonProgressAsync(Guid classId, Guid lessonId,
        UpdateLessonProgressRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (request.Status is not (LessonProgressStatus.InProgress or LessonProgressStatus.Completed))
        {
            return Results.BadRequest(new { error = "Lesson progress status is invalid." });
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot receive lesson progress." });
        }

        if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var lesson = await session.LoadAsync<Lesson>(lessonId, cancellationToken);
        if (lesson is null || lesson.CourseId != learningClass.CourseId ||
            lesson.Status != AcademicContentStatus.Published)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var id = LearningConstants.LessonProgressId(lessonId, classId, actor.UserId);
        var record = await session.LoadAsync<StudentLessonProgressRecord>(id, cancellationToken);
        if (record is not null && record.Status == LessonProgressStatus.Completed &&
            request.Status == LessonProgressStatus.InProgress)
        {
            return Results.Conflict(new { error = "Completed lesson progress cannot move backwards." });
        }

        var isNew = record is null;
        record ??= new StudentLessonProgressRecord
        {
            Id = id,
            OrganizationId = actor.OrganizationId,
            LearningClassId = classId,
            CourseId = learningClass.CourseId,
            LessonId = lessonId,
            StudentId = actor.UserId,
            StartedAt = now
        };
        record.Status = request.Status;
        record.CompletedAt = request.Status == LessonProgressStatus.Completed
            ? record.CompletedAt ?? now
            : null;
        record.UpdatedAt = now;
        if (isNew)
        {
            session.Insert(record);
        }
        else
        {
            session.Update(record);
        }

        await session.SaveChangesAsync(cancellationToken);
        try
        {
            await messageBus.PublishAsync(new LessonProgressChangedForReporting(
                actor.OrganizationId, classId, actor.UserId));
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("LearningPlatform.Learning.Progress").LogWarning(exception,
                "Unable to enqueue lesson progress reporting refresh for {ClassId}/{StudentId}.",
                classId, actor.UserId);
        }

        return Results.Ok(LessonProgressEntryResponse.From(actor.UserId, lesson, record));
    }

    private static async Task<IResult> ListResourcesAsync(Guid classId, Guid? lessonId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
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

        var canManage = await CanManageClassAsync(actor, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<LearningResource>()
            .Where(resource => resource.LearningClassId == classId);
        if (lessonId is { } requestedLessonId)
        {
            query = query.Where(resource => resource.LessonId == requestedLessonId);
        }

        if (!canManage)
        {
            query = query.Where(resource => resource.Status == LearningResourceStatus.Published);
        }

        var resources = await query.OrderBy(resource => resource.CreatedAt)
            .ToListAsync(cancellationToken);
        if (!canManage)
        {
            var lessonIds = resources.Where(resource => resource.LessonId is not null)
                .Select(resource => resource.LessonId!.Value)
                .Distinct()
                .ToArray();
            if (lessonIds.Length > 0)
            {
                var publishedLessonIds = await session.Query<Lesson>()
                    .Where(lesson => lessonIds.Contains(lesson.Id) &&
                        lesson.Status == AcademicContentStatus.Published)
                    .Select(lesson => lesson.Id)
                    .ToListAsync(cancellationToken);
                resources = resources.Where(resource => resource.LessonId is null ||
                    publishedLessonIds.Contains(resource.LessonId.Value)).ToList();
            }
        }
        return Results.Ok(resources.Select(resource => LearningResourceResponse.From(resource)));
    }

    private static async Task<IResult> GetResourceAsync(Guid resourceId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, ILearningResourceStorage storage,
        CancellationToken cancellationToken)
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

        var canManage = await CanManageClassAsync(actor!, resource.LearningClassId, academics,
            cancellationToken);
        if (!canManage && (resource.Status != LearningResourceStatus.Published ||
                !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                    resource.LearningClassId, actor.UserId, cancellationToken) ||
                resource.LessonId is { } lessonId &&
                await session.LoadAsync<Lesson>(lessonId, cancellationToken) is not
                { Status: AcademicContentStatus.Published }))
        {
            return Results.Forbid();
        }

        string? accessUrl = resource.ExternalUrl;
        if (resource.StorageKey is { Length: > 0 } storageKey)
        {
            try
            {
                accessUrl = await storage.GetReadUrlAsync(actor!.OrganizationId, storageKey,
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        return Results.Ok(LearningResourceResponse.From(resource, accessUrl));
    }

    private static async Task<IResult> CreateResourceAsync(Guid classId,
        CreateLearningResourceRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        try
        {
            var resource = await BuildResourceAsync(request.LessonId, request.LiveSessionId,
                request.Title, request.Description, request.Type, request.ExternalUrl,
                request.DurationSeconds, classId, learningClass.CourseId, actor, session,
                cancellationToken);
            session.Insert(resource);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/learning/resources/{resource.Id:D}",
                LearningResourceResponse.From(resource));
        }
        catch (LearningResourceValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
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

    private static Task<IResult> PublishResourceAsync(Guid resourceId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        ChangeResourceStatusAsync(resourceId, LearningResourceStatus.Published, context, session,
            academics, cancellationToken);

    private static Task<IResult> ArchiveResourceAsync(Guid resourceId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        ChangeResourceStatusAsync(resourceId, LearningResourceStatus.Archived, context, session,
            academics, cancellationToken);

    private static async Task<IResult> ChangeResourceStatusAsync(Guid resourceId,
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

    private static async Task<LearningResource> BuildResourceAsync(Guid? lessonId,
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

    private static async Task ValidateReferencesAsync(LearningResource resource,
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

    private static void ValidateResourceFields(LearningResource resource)
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

    private static bool IsAllowedContentType(LearningResourceType type, string contentType) => type switch
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

    private static string NormalizeContentType(IFormFile file)
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

    private static async Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken) =>
        actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
        await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
            cancellationToken) ||
        await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
            cancellationToken);
}

public sealed record LearningProgressResponse(Guid LearningClassId,
    LearningProgressEntryResponse[] Entries);

public sealed record CourseProgressResponse(Guid LearningClassId, Guid CourseId, int TotalLessons,
    LessonProgressEntryResponse[] Entries)
{
    public decimal CompletionPercent
    {
        get
        {
            if (TotalLessons == 0)
            {
                return 0m;
            }

            var groups = Entries.GroupBy(entry => entry.StudentId).ToArray();
            return groups.Length == 0
                ? 0m
                : decimal.Round(groups.Sum(group => group.Count(entry =>
                    entry.Status == LessonProgressStatus.Completed)) * 100m /
                    (groups.Length * TotalLessons), 2);
        }
    }
}

public sealed record LessonProgressEntryResponse(Guid StudentId, Guid LessonId, string LessonTitle,
    LessonProgressStatus? Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    DateTimeOffset? UpdatedAt)
{
    public static LessonProgressEntryResponse From(Guid studentId, Lesson lesson,
        StudentLessonProgressRecord? record) => new(studentId, lesson.Id, lesson.Title,
        record?.Status, record?.StartedAt, record?.CompletedAt, record?.UpdatedAt);
}

public sealed record UpdateLessonProgressRequest(LessonProgressStatus Status);

public sealed record LearningProgressEntryResponse(Guid StudentId, Guid ActivityId,
    string ActivityTitle, decimal Score, decimal MaxScore, decimal Completion,
    LearningProgressStatus Status, DateTimeOffset? CompletedAt, int Version)
{
    public static LearningProgressEntryResponse From(StudentProgressRecord record) => new(record.StudentId,
        record.ActivityId, record.ActivityTitle, record.Score, record.MaxScore, record.Completion,
        record.Status, record.CompletedAt, record.Version);
}

public sealed record CreateLearningResourceRequest(Guid? LessonId, Guid? LiveSessionId,
    string Title, string? Description, LearningResourceType Type, string? ExternalUrl,
    int? DurationSeconds);

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

public sealed class LearningResourceValidationException(string message) : Exception(message);
