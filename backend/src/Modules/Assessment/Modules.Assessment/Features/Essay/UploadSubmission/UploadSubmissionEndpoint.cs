using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Storage;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Contracts;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Api.EssayEndpoints;

namespace LearningPlatform.Assessment.Features.Essay.UploadSubmission;

internal static class UploadSubmissionEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/groups/{groupId:guid}/submissions",
                UploadSubmissionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
    }


    private static async Task<IResult> UploadSubmissionAsync(Guid assignmentId, Guid groupId,
        HttpRequest request, HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        IFileStorage storage, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (assignment is null || group is null || group.EssayAssignmentId != assignmentId)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, group.LearningClassId, academics,
            cancellationToken);
        if (assignment.Status != EssayAssignmentStatus.Published ||
            group.Status != EssayGroupStatus.Active ||
            assignment.Deadline is { } deadline && DateTimeOffset.UtcNow > deadline ||
            (!canManage && !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken)))
        {
            return Results.Forbid();
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(assignmentId, groupId),
            cancellationToken);
        if (topic is null || topic.Status != EssayTopicStatus.Locked ||
            string.IsNullOrWhiteSpace(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "The group needs a locked canonical topic before submitting." });
        }

        var latestAccepted = await session.Query<EssaySubmission>()
            .Where(submission => submission.EssayGroupId == groupId &&
                (submission.Status == EssaySubmissionStatus.Submitted ||
                 submission.Status == EssaySubmissionStatus.Graded))
            .OrderByDescending(submission => submission.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestAccepted is not null && !assignment.AllowResubmission)
        {
            return Results.Conflict(new { error = "This assignment does not allow resubmission." });
        }

        if (latestAccepted?.Status == EssaySubmissionStatus.Graded)
        {
            return Results.Conflict(new { error = "A graded report cannot be resubmitted." });
        }

        IFormFile? file;
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { error = "The multipart report could not be read." });
        }

        if (file is null)
        {
            return Results.BadRequest(new { error = "A report file is required." });
        }

        var contentType = NormalizeContentType(file);
        var policy = assignment.ValidationPolicy ?? new ReportValidationPolicy();
        policy.AllowedFileTypes ??= [];
        policy.RequiredSections ??= [];
        var errors = new List<string>();
        if (!policy.AllowedFileTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("The report file type is not allowed for this assignment.");
        }

        if (file.Length <= 0 || file.Length > policy.MaxFileBytes)
        {
            errors.Add($"The report file must be between 1 and {policy.MaxFileBytes} bytes.");
        }

        ExtractedReport? report = null;
        if (errors.Count == 0)
        {
            try
            {
                await using var input = file.OpenReadStream();
                report = await EssayRules.ExtractAsync(input, contentType, cancellationToken);
            }
            catch (AssessmentValidationException exception)
            {
                errors.Add(exception.Message);
            }
        }

        var missingSections = new List<string>();
        if (report is not null)
        {
            if (report.PageCount is null && policy.MinimumPages is not null)
            {
                errors.Add("The report page count could not be determined.");
            }
            else if (policy.MinimumPages is { } pages && report.PageCount < pages)
            {
                errors.Add($"The report has {report.PageCount} pages; at least {pages} are required.");
            }

            if (policy.MinimumWords is { } words && report.WordCount < words)
            {
                errors.Add($"The report has {report.WordCount} words; at least {words} are required.");
            }

            if (policy.MinimumCharacters is { } characters && report.CharacterCount < characters)
            {
                errors.Add($"The report has {report.CharacterCount} characters; at least {characters} are required.");
            }

            if (report.WordCount == 0 || report.CharacterCount == 0)
            {
                errors.Add("The report content is empty or nearly empty.");
            }

            foreach (var section in policy.RequiredSections)
            {
                if (!EssayRules.NormalizeTopic(report.Text).Contains(EssayRules.NormalizeTopic(section),
                        StringComparison.Ordinal))
                {
                    missingSections.Add(section);
                }
            }

            if (missingSections.Count > 0)
            {
                errors.Add($"Required sections are missing: {string.Join(", ", missingSections)}.");
            }

            if (policy.RequireTopicMatch &&
                !string.Equals(EssayRules.NormalizeTopic(report.Title),
                    EssayRules.NormalizeTopic(topic.CanonicalTitle), StringComparison.Ordinal))
            {
                errors.Add("The report title does not match the approved canonical topic.");
            }
        }

        var latestVersion = await session.Query<EssaySubmission>()
            .Where(submission => submission.EssayGroupId == groupId)
            .OrderByDescending(submission => submission.Version)
            .Select(submission => submission.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var version = latestVersion + 1;
        var now = DateTimeOffset.UtcNow;
        var submissionId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var validationId = Guid.NewGuid();
        if (report is not null)
        {
            var storageKey = $"{actor!.OrganizationId:D}/essay/{assignmentId:D}/{groupId:D}/{assetId:D}/{Path.GetFileName(file.FileName)}";
            await using var upload = new MemoryStream(report.Bytes, writable: false);
            try
            {
                await storage.PutAsync(actor.OrganizationId, storageKey, upload, contentType,
                    cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            session.Insert(new FileAsset
            {
                Id = assetId,
                OrganizationId = actor.OrganizationId,
                StorageKey = storageKey,
                FileName = Path.GetFileName(file.FileName),
                ContentType = contentType,
                Size = report.Bytes.LongLength,
                CreatedBy = actor.UserId,
                CreatedAt = now
            });

            if (errors.Count == 0 && latestAccepted is not null)
            {
                latestAccepted.Status = EssaySubmissionStatus.Superseded;
                session.Store(latestAccepted);
            }
        }
        else
        {
            // A rejected upload still records its validation attempt, but never
            // becomes a final submission visible in the gradebook.
            assetId = Guid.Empty;
        }

        var submission = new EssaySubmission
        {
            Id = submissionId,
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignmentId,
            EssayGroupId = groupId,
            FileAssetId = assetId,
            Version = version,
            Status = errors.Count == 0 ? EssaySubmissionStatus.Submitted : EssaySubmissionStatus.Rejected,
            UploadedAt = now,
            SubmittedAt = errors.Count == 0 ? now : null,
            ValidationResultId = validationId
        };
        session.Insert(submission);
        session.Insert(new ReportValidationResult
        {
            Id = validationId,
            OrganizationId = actor.OrganizationId,
            EssaySubmissionId = submissionId,
            IsValid = errors.Count == 0,
            ExtractedTitle = report?.Title,
            NormalizedTitle = report is null ? null : EssayRules.NormalizeTopic(report.Title),
            PageCount = report?.PageCount,
            WordCount = report?.WordCount,
            CharacterCount = report?.CharacterCount,
            MissingSections = missingSections.ToArray(),
            Errors = errors.ToArray(),
            ValidatedAt = now
        });
        await session.SaveChangesAsync(cancellationToken);

        if (errors.Count == 0)
        {
            await PublishSubmissionReportingRefreshAsync(messageBus, loggerFactory,
                actor.OrganizationId, assignment.LearningClassId, actor.UserId);
        }

        return errors.Count == 0
            ? Results.Created($"/api/assessment/essay-submissions/{submissionId:D}",
                EssaySubmissionResponse.From(submission, errors.ToArray()))
            : Results.BadRequest(new
            {
                error = "The report failed pre-submission validation.",
                submission = EssaySubmissionResponse.From(submission, errors.ToArray())
            });
    }

}
