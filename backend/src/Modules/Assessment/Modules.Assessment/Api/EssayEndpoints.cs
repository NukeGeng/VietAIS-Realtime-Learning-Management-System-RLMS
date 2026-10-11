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

namespace LearningPlatform.Assessment.Api;

public static class EssayEndpoints
{
    public static void MapEssayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Assessment.Features.Essay.ListAssignments.ListAssignmentsEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.CreateAssignment.CreateAssignmentEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.GetAssignment.GetAssignmentEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.PublishAssignment.PublishAssignmentEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.CloseAssignment.CloseAssignmentEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.CreateGroup.CreateGroupEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.AddGroupMember.AddGroupMemberEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.ListGroups.ListGroupsEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.RegisterTopic.RegisterTopicEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.RequestTopicChange.RequestTopicChangeEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.GetTopic.GetTopicEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.ApproveTopic.ApproveTopicEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.RequestTopicChanges.RequestTopicChangesEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.RejectTopic.RejectTopicEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.LockTopic.LockTopicEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.ListSubmissions.ListSubmissionsEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.GetSubmissionFile.GetSubmissionFileEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.UploadSubmission.UploadSubmissionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.GradeEssaySubmission.GradeEssaySubmissionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.PublishEssayGrade.PublishEssayGradeEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Essay.GetEssayResult.GetEssayResultEndpoint.Map(endpoints);
    }

    internal static async Task<IResult> ChangeAssignmentStatusAsync(Guid assignmentId,
        EssayAssignmentStatus target, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, assignment.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var valid = (assignment.Status, target) switch
        {
            (EssayAssignmentStatus.Draft, EssayAssignmentStatus.Published) => true,
            (EssayAssignmentStatus.Published, EssayAssignmentStatus.Closed) => true,
            _ => false
        };
        if (!valid)
        {
            return Results.Conflict(new { error = "The essay assignment cannot make that state transition." });
        }

        assignment.Status = target;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        session.Store(assignment);
        await session.SaveChangesAsync(cancellationToken);
        await PublishEssayReportingRefreshAsync(messageBus, loggerFactory, assignment);
        return Results.Ok(EssayAssignmentResponse.From(assignment));
    }


    internal static async Task PublishEssayReportingRefreshAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, EssayAssignment assignment)
    {
        try
        {
            await messageBus.PublishAsync(new EssayAssignmentChangedForReporting(
                assignment.OrganizationId, assignment.LearningClassId, assignment.Id));
        }
        catch (Exception exception)
        {
            // The assignment lifecycle change is durable even if the asynchronous
            // reporting queue is temporarily unavailable.
            loggerFactory.CreateLogger("LearningPlatform.Assessment.Essay").LogWarning(exception,
                "Unable to enqueue essay reporting refresh for {AssignmentId}.", assignment.Id);
        }
    }


    internal static async Task<IResult> ReviewTopicAsync(Guid topicId, EssayTopicStatus target,
        HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var topic = await session.LoadAsync<EssayTopic>(topicId, cancellationToken);
        if (topic is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, await FindGroupClassAsync(session, topic.EssayGroupId,
                cancellationToken), academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (topic.Status is not (EssayTopicStatus.PendingReview or EssayTopicStatus.ChangesRequested or
                EssayTopicStatus.Approved))
        {
            return Results.Conflict(new { error = "The topic is not awaiting this review action." });
        }

        var changeRequest = await session.Query<EssayTopicChangeRequest>()
            .Where(value => value.EssayTopicId == topic.Id &&
                value.Status == EssayTopicChangeRequestStatus.PendingReview)
            .OrderByDescending(value => value.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        topic.Status = target;
        topic.ReviewedBy = actor!.UserId;
        topic.ReviewedAt = DateTimeOffset.UtcNow;
        if (target == EssayTopicStatus.Approved)
        {
            topic.CanonicalTitle = EssayRules.CanonicalizeTitle(topic.ProposedTitle);
        }
        else if (target == EssayTopicStatus.Rejected && changeRequest is not null)
        {
            topic.CanonicalTitle = changeRequest.PreviousCanonicalTitle;
            topic.Status = changeRequest.PreviousStatus;
        }

        if (changeRequest is not null)
        {
            changeRequest.Status = target switch
            {
                EssayTopicStatus.Approved => EssayTopicChangeRequestStatus.Approved,
                EssayTopicStatus.Rejected => EssayTopicChangeRequestStatus.Rejected,
                _ => EssayTopicChangeRequestStatus.PendingReview
            };
            changeRequest.ReviewedBy = actor.UserId;
            changeRequest.ReviewedAt = topic.ReviewedAt;
            session.Store(changeRequest);
        }

        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }


    internal static async Task PublishSubmissionReportingRefreshAsync(IMessageBus messageBus,
        ILoggerFactory loggerFactory, Guid organizationId, Guid classId, Guid studentId)
    {
        try
        {
            await messageBus.PublishAsync(new SubmissionChangedForReporting(organizationId, classId,
                studentId));
        }
        catch (Exception exception)
        {
            // Accepted submission metadata is durable even if the asynchronous
            // reporting queue is temporarily unavailable.
            loggerFactory.CreateLogger("LearningPlatform.Assessment.Submission").LogWarning(exception,
                "Unable to enqueue essay submission reporting refresh for {ClassId}/{StudentId}.",
                classId, studentId);
        }
    }


    internal static async Task<Guid> FindGroupClassAsync(IQuerySession session, Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        return group?.LearningClassId ?? Guid.Empty;
    }


    internal static async Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return classId != Guid.Empty &&
            (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
             await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                 cancellationToken) ||
             await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken));
    }


    internal static async Task<bool> IsActiveGroupMemberAsync(AuthenticatedActor actor,
        EssayGroup group, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return group.Members.Any(member => member.StudentId == actor.UserId) &&
            await academics.IsStudentEnrolledAsync(actor.OrganizationId, group.LearningClassId,
                actor.UserId, cancellationToken);
    }


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
            _ => (file.ContentType ?? "").Trim().ToLowerInvariant()
        };
    }

}
