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
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/essay-assignments",
                ListAssignmentsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/classes/{classId:guid}/essay-assignments",
                CreateAssignmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapGet("/api/assessment/essay-assignments/{assignmentId:guid}", GetAssignmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/publish",
                PublishAssignmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/close",
                CloseAssignmentAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/groups",
                CreateGroupAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-groups/{groupId:guid}/members",
                AddGroupMemberAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapGet("/api/assessment/essay-assignments/{assignmentId:guid}/groups",
                ListGroupsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-groups/{groupId:guid}/topic", RegisterTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-groups/{groupId:guid}/topic/change-request",
                RequestTopicChangeAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapGet("/api/assessment/essay-groups/{groupId:guid}/topic", GetTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-topics/{topicId:guid}/approve", ApproveTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-topics/{topicId:guid}/request-changes",
                RequestTopicChangesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-topics/{topicId:guid}/reject", RejectTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-topics/{topicId:guid}/lock", LockTopicAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
        endpoints.MapGet("/api/assessment/essay-groups/{groupId:guid}/submissions",
                ListSubmissionsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapGet("/api/assessment/essay-submissions/{submissionId:guid}/file",
                GetSubmissionFileAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
        endpoints.MapPost("/api/assessment/essay-assignments/{assignmentId:guid}/groups/{groupId:guid}/submissions",
                UploadSubmissionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.SubmitAssessmentActivities);
        endpoints.MapPost("/api/assessment/essay-submissions/{submissionId:guid}/grade",
                GradeEssaySubmissionAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
        endpoints.MapPost("/api/assessment/essay-grade-entries/{gradeEntryId:guid}/publish",
                PublishEssayGradeAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.GradeAssessmentSubmissions);
        endpoints.MapGet("/api/assessment/essay-assignments/{assignmentId:guid}/groups/{groupId:guid}/result",
                GetEssayResultAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAssessmentResults);
    }

    private static async Task<IResult> ListAssignmentsAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var canManage = await CanManageClassAsync(actor!, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor!.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var assignments = await session.Query<EssayAssignment>()
            .Where(assignment => assignment.LearningClassId == classId &&
                (canManage || assignment.Status != EssayAssignmentStatus.Draft))
            .OrderByDescending(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(assignments.Select(EssayAssignmentResponse.From));
    }

    private static async Task<IResult> CreateAssignmentAsync(Guid classId,
        CreateEssayAssignmentRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken);
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
            return Results.Conflict(new { error = "A completed or archived class cannot receive a new essay assignment." });
        }

        try
        {
            var policy = request.ValidationPolicy ?? new ReportValidationPolicy();
            policy.AllowedFileTypes ??= [];
            policy.RequiredSections ??= [];
            EssayRules.ValidatePolicy(policy);
            var title = EssayRules.RequireTitle(request.Title);
            var description = request.Description?.Trim() ?? "";
            if (description.Length > EssayRules.MaxDescriptionLength || request.MaxScore <= 0 ||
                request.MaxScore > 100_000 || request.MaxGroupSize is < 1 or > 100)
            {
                throw new AssessmentValidationException("Essay score, description or group size is invalid.");
            }

            var now = DateTimeOffset.UtcNow;
            var assignment = new EssayAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                CourseId = learningClass.CourseId,
                Title = title,
                Description = description,
                Deadline = request.Deadline,
                MaxScore = request.MaxScore,
                MaxGroupSize = request.MaxGroupSize,
                AllowResubmission = request.AllowResubmission,
                Status = EssayAssignmentStatus.Draft,
                ValidationPolicy = policy,
                CreatedBy = actor.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(assignment);
            session.Insert(new GradeItem
            {
                Id = EssayConstants.GradeItemId(assignment.Id),
                OrganizationId = assignment.OrganizationId,
                LearningClassId = assignment.LearningClassId,
                ActivityId = Guid.Empty,
                EssayAssignmentId = assignment.Id,
                CourseId = assignment.CourseId,
                Title = assignment.Title,
                MaxScore = assignment.MaxScore,
                Weight = 1m,
                Status = GradeItemStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/assessment/essay-assignments/{assignment.Id:D}",
                EssayAssignmentResponse.From(assignment));
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static async Task<IResult> GetAssignmentAsync(Guid assignmentId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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

        var canManage = await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
            cancellationToken);
        if (!canManage && (assignment.Status == EssayAssignmentStatus.Draft ||
                !await academics.IsStudentEnrolledAsync(actor!.OrganizationId, assignment.LearningClassId,
                    actor.UserId, cancellationToken)))
        {
            return Results.Forbid();
        }

        return Results.Ok(EssayAssignmentResponse.From(assignment));
    }

    private static Task<IResult> PublishAssignmentAsync(Guid assignmentId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeAssignmentStatusAsync(assignmentId, EssayAssignmentStatus.Published, context, session,
            academics, messageBus, loggerFactory, cancellationToken);

    private static Task<IResult> CloseAssignmentAsync(Guid assignmentId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeAssignmentStatusAsync(assignmentId, EssayAssignmentStatus.Closed, context, session,
            academics, messageBus, loggerFactory, cancellationToken);

    private static async Task<IResult> ChangeAssignmentStatusAsync(Guid assignmentId,
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

    private static async Task PublishEssayReportingRefreshAsync(IMessageBus messageBus,
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

    private static async Task<IResult> CreateGroupAsync(Guid assignmentId,
        CreateEssayGroupRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        if (assignment.Status != EssayAssignmentStatus.Published ||
            !await academics.IsStudentEnrolledAsync(actor!.OrganizationId, assignment.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var existing = await session.Query<EssayGroup>()
            .Where(group => group.EssayAssignmentId == assignmentId &&
                group.Members.Any(member => member.StudentId == actor.UserId) &&
                group.Status == EssayGroupStatus.Active)
            .AnyAsync(cancellationToken);
        if (existing)
        {
            return Results.Conflict(new { error = "A student can only belong to one active group for an assignment." });
        }

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200)
        {
            return Results.BadRequest(new { error = "A group name is required and must be at most 200 characters." });
        }

        var now = DateTimeOffset.UtcNow;
        var group = new EssayGroup
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignmentId,
            LearningClassId = assignment.LearningClassId,
            Name = name,
            Status = EssayGroupStatus.Active,
            Members = [new EssayGroupMember(actor.UserId, now)],
            CreatedAt = now,
            UpdatedAt = now
        };
        session.Insert(group);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/assessment/essay-groups/{group.Id:D}", EssayGroupResponse.From(group));
    }

    private static async Task<IResult> AddGroupMemberAsync(Guid groupId, AddEssayGroupMemberRequest request,
        HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(group.EssayAssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken);
        var isMember = await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken);
        if (!canManage && !isMember)
        {
            return Results.Forbid();
        }

        if (assignment.Status != EssayAssignmentStatus.Published ||
            group.Status != EssayGroupStatus.Active || group.Members.Count >= assignment.MaxGroupSize)
        {
            return Results.Conflict(new { error = "The group cannot accept another member." });
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(
            assignment.Id, group.Id), cancellationToken);
        var hasSubmission = await session.Query<EssaySubmission>()
            .Where(value => value.EssayGroupId == group.Id &&
                value.Status != EssaySubmissionStatus.Rejected)
            .AnyAsync(cancellationToken);
        if (topic?.Status == EssayTopicStatus.Locked || hasSubmission)
        {
            return Results.Conflict(new { error = "Group membership is locked after the topic or report is finalized." });
        }

        if (!await academics.IsStudentEnrolledAsync(actor!.OrganizationId, group.LearningClassId,
                request.StudentId, cancellationToken))
        {
            return Results.BadRequest(new { error = "The student is not actively enrolled in this class." });
        }

        var inAnotherGroup = await session.Query<EssayGroup>()
            .Where(value => value.EssayAssignmentId == group.EssayAssignmentId && value.Id != group.Id &&
                value.Status == EssayGroupStatus.Active &&
                value.Members.Any(member => member.StudentId == request.StudentId))
            .AnyAsync(cancellationToken);
        if (inAnotherGroup || group.Members.Any(member => member.StudentId == request.StudentId))
        {
            return Results.Conflict(new { error = "The student is already in a group for this assignment." });
        }

        group.Members.Add(new EssayGroupMember(request.StudentId, DateTimeOffset.UtcNow));
        group.UpdatedAt = DateTimeOffset.UtcNow;
        session.Store(group);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayGroupResponse.From(group));
    }

    private static async Task<IResult> ListGroupsAsync(Guid assignmentId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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

        var canManage = await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
            cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                assignment.LearningClassId, actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<EssayGroup>().Where(group => group.EssayAssignmentId == assignmentId);
        if (!canManage)
        {
            query = query.Where(group => group.Members.Any(member => member.StudentId == actor!.UserId));
        }

        var groups = await query.OrderBy(group => group.Name).ToListAsync(cancellationToken);
        return Results.Ok(groups.Select(EssayGroupResponse.From));
    }

    private static async Task<IResult> RegisterTopicAsync(Guid groupId, RegisterEssayTopicRequest request,
        HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(group.EssayAssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken) &&
            !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (assignment.Status != EssayAssignmentStatus.Published ||
            group.Status != EssayGroupStatus.Active)
        {
            return Results.Conflict(new { error = "The essay assignment or group is not accepting topic changes." });
        }

        string proposed;
        try
        {
            proposed = EssayRules.RequireTitle(request.ProposedTitle);
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        var topicId = EssayConstants.TopicId(assignment.Id, group.Id);
        var topic = await session.LoadAsync<EssayTopic>(topicId, cancellationToken);
        if (topic is not null && topic.Status is (EssayTopicStatus.Approved or EssayTopicStatus.Locked))
        {
            return Results.Conflict(new { error = "An approved topic must use the topic change workflow." });
        }

        var now = DateTimeOffset.UtcNow;
        topic ??= new EssayTopic
        {
            Id = topicId,
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignment.Id,
            EssayGroupId = group.Id
        };
        topic.ProposedTitle = proposed;
        topic.CanonicalTitle = null;
        topic.Status = EssayTopicStatus.PendingReview;
        topic.SubmittedAt = now;
        topic.ReviewedBy = null;
        topic.ReviewedAt = null;
        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }

    private static async Task<IResult> RequestTopicChangeAsync(Guid groupId,
        RegisterEssayTopicRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(group.EssayAssignmentId,
            cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken) &&
            !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (assignment.Status != EssayAssignmentStatus.Published ||
            group.Status != EssayGroupStatus.Active)
        {
            return Results.Conflict(new { error = "The essay assignment or group is not accepting topic changes." });
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(
            assignment.Id, group.Id), cancellationToken);
        if (topic is null || topic.Status is not (EssayTopicStatus.Approved or EssayTopicStatus.Locked) ||
            string.IsNullOrWhiteSpace(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "Only an approved or locked topic can be changed." });
        }

        string proposed;
        try
        {
            proposed = EssayRules.RequireTitle(request.ProposedTitle);
        }
        catch (AssessmentValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }

        if (EssayRules.NormalizeTopic(proposed) == EssayRules.NormalizeTopic(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "The new topic must differ from the locked canonical topic." });
        }

        var hasPending = await session.Query<EssayTopicChangeRequest>()
            .Where(value => value.EssayTopicId == topic.Id &&
                value.Status == EssayTopicChangeRequestStatus.PendingReview)
            .AnyAsync(cancellationToken);
        if (hasPending)
        {
            return Results.Conflict(new { error = "A topic change is already awaiting teacher review." });
        }

        var now = DateTimeOffset.UtcNow;
        session.Insert(new EssayTopicChangeRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = actor!.OrganizationId,
            EssayAssignmentId = assignment.Id,
            EssayGroupId = group.Id,
            EssayTopicId = topic.Id,
            PreviousCanonicalTitle = topic.CanonicalTitle!,
            PreviousStatus = topic.Status,
            ProposedTitle = proposed,
            Status = EssayTopicChangeRequestStatus.PendingReview,
            RequestedBy = actor.UserId,
            RequestedAt = now
        });
        topic.ProposedTitle = proposed;
        topic.CanonicalTitle = null;
        topic.Status = EssayTopicStatus.PendingReview;
        topic.ReviewedBy = null;
        topic.ReviewedAt = null;
        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }

    private static async Task<IResult> GetTopicAsync(Guid groupId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken) &&
            !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var topic = await session.LoadAsync<EssayTopic>(EssayConstants.TopicId(
            group.EssayAssignmentId, group.Id), cancellationToken);
        return topic is null ? Results.NotFound() : Results.Ok(EssayTopicResponse.From(topic));
    }

    private static Task<IResult> ApproveTopicAsync(Guid topicId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        ReviewTopicAsync(topicId, EssayTopicStatus.Approved, context, session, academics, cancellationToken);

    private static Task<IResult> RequestTopicChangesAsync(Guid topicId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        ReviewTopicAsync(topicId, EssayTopicStatus.ChangesRequested, context, session, academics,
            cancellationToken);

    private static Task<IResult> RejectTopicAsync(Guid topicId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken) =>
        ReviewTopicAsync(topicId, EssayTopicStatus.Rejected, context, session, academics, cancellationToken);

    private static async Task<IResult> ReviewTopicAsync(Guid topicId, EssayTopicStatus target,
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

    private static async Task<IResult> LockTopicAsync(Guid topicId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, CancellationToken cancellationToken)
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

        var classId = await FindGroupClassAsync(session, topic.EssayGroupId, cancellationToken);
        if (!await CanManageClassAsync(actor!, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (topic.Status != EssayTopicStatus.Approved || string.IsNullOrWhiteSpace(topic.CanonicalTitle))
        {
            return Results.Conflict(new { error = "Only an approved topic can be locked." });
        }

        topic.Status = EssayTopicStatus.Locked;
        topic.ReviewedBy = actor!.UserId;
        topic.ReviewedAt = DateTimeOffset.UtcNow;
        session.Store(topic);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(EssayTopicResponse.From(topic));
    }

    private static async Task<IResult> ListSubmissionsAsync(Guid groupId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, group.LearningClassId, academics, cancellationToken) &&
            !await IsActiveGroupMemberAsync(actor!, group, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var submissions = await session.Query<EssaySubmission>()
            .Where(submission => submission.EssayGroupId == groupId)
            .OrderByDescending(submission => submission.Version)
            .ToListAsync(cancellationToken);
        var validationIds = submissions.Where(value => value.ValidationResultId is not null)
            .Select(value => value.ValidationResultId!.Value).ToArray();
        var validationResults = validationIds.Length == 0
            ? []
            : await session.Query<ReportValidationResult>()
                .Where(value => validationIds.Contains(value.Id))
                .ToListAsync(cancellationToken);
        var errorsBySubmission = validationResults.ToDictionary(value => value.EssaySubmissionId,
            value => value.Errors);
        return Results.Ok(submissions.Select(submission => EssaySubmissionResponse.From(submission,
            errorsBySubmission.GetValueOrDefault(submission.Id, []))));
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

    private static async Task PublishSubmissionReportingRefreshAsync(IMessageBus messageBus,
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

    private static async Task<IResult> GetSubmissionFileAsync(Guid submissionId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        IFileStorage storage, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var submission = await session.LoadAsync<EssaySubmission>(submissionId, cancellationToken);
        if (submission is null || submission.FileAssetId == Guid.Empty)
        {
            return Results.NotFound();
        }

        var group = await session.LoadAsync<EssayGroup>(submission.EssayGroupId, cancellationToken);
        var assignment = await session.LoadAsync<EssayAssignment>(submission.EssayAssignmentId,
            cancellationToken);
        if (group is null || assignment is null || group.EssayAssignmentId != assignment.Id)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, group.LearningClassId, academics,
            cancellationToken);
        if (!canManage && !await IsActiveGroupMemberAsync(actor!, group, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var asset = await session.LoadAsync<FileAsset>(submission.FileAssetId, cancellationToken);
        if (asset is null || asset.OrganizationId != actor!.OrganizationId ||
            string.IsNullOrWhiteSpace(asset.StorageKey))
        {
            return Results.NotFound();
        }

        try
        {
            var accessUrl = await storage.GetReadUrlAsync(actor.OrganizationId, asset.StorageKey,
                cancellationToken);
            return accessUrl is null
                ? Results.NotFound()
                : Results.Ok(new EssaySubmissionFileResponse(submission.Id, asset.FileName,
                    asset.ContentType, asset.Size, accessUrl));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> GradeEssaySubmissionAsync(Guid submissionId,
        GradeEssaySubmissionRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var submission = await session.LoadAsync<EssaySubmission>(submissionId, cancellationToken);
        if (submission is null)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(submission.EssayAssignmentId,
            cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, assignment.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (submission.Status == EssaySubmissionStatus.Rejected)
        {
            return Results.Conflict(new { error = "A rejected report cannot be graded." });
        }

        var latestSubmission = await session.Query<EssaySubmission>()
            .Where(value => value.EssayGroupId == submission.EssayGroupId &&
                value.Status != EssaySubmissionStatus.Rejected)
            .OrderByDescending(value => value.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestSubmission?.Id != submission.Id)
        {
            return Results.Conflict(new { error = "Only the latest report version can be graded." });
        }

        if (request.Score < 0m || request.Score > assignment.MaxScore)
        {
            return Results.BadRequest(new { error = "The essay score must be within the assignment points." });
        }

        var now = DateTimeOffset.UtcNow;
        var gradeItem = await session.LoadAsync<GradeItem>(EssayConstants.GradeItemId(assignment.Id),
            cancellationToken);
        if (gradeItem is null)
        {
            gradeItem = new GradeItem
            {
                Id = EssayConstants.GradeItemId(assignment.Id),
                OrganizationId = assignment.OrganizationId,
                LearningClassId = assignment.LearningClassId,
                ActivityId = Guid.Empty,
                EssayAssignmentId = assignment.Id,
                CourseId = assignment.CourseId,
                Title = assignment.Title,
                MaxScore = assignment.MaxScore,
                Weight = 1m,
                Status = GradeItemStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(gradeItem);
        }

        var group = await session.LoadAsync<EssayGroup>(submission.EssayGroupId, cancellationToken);
        var members = group?.Members.OrderBy(member => member.JoinedAt).ToArray() ?? [];
        if (members.Length == 0)
        {
            return Results.Conflict(new { error = "The essay group has no active member." });
        }

        GradeEntry? representative = null;
        foreach (var member in members)
        {
            var entry = await session.LoadAsync<GradeEntry>(EssayConstants.GradeEntryId(assignment.Id,
                submission.EssayGroupId, member.StudentId), cancellationToken) ?? new GradeEntry
                {
                    Id = EssayConstants.GradeEntryId(assignment.Id, submission.EssayGroupId,
                        member.StudentId),
                    OrganizationId = assignment.OrganizationId,
                    LearningClassId = assignment.LearningClassId,
                    ActivityId = Guid.Empty,
                    EssayAssignmentId = assignment.Id,
                    EssayGroupId = submission.EssayGroupId,
                    GradeItemId = gradeItem.Id,
                    StudentId = member.StudentId,
                    SourceSubmissionId = submission.Id,
                    AttemptNumber = 1,
                    MaxScore = assignment.MaxScore,
                    Version = 0
                };
            entry.Score = request.Score;
            entry.MaxScore = assignment.MaxScore;
            entry.Status = GradeEntryStatus.Draft;
            entry.Source = GradeSource.Manual;
            entry.IsComplete = true;
            entry.Feedback = GradingRules.NormalizeFeedback(request.Feedback);
            entry.GradedBy = actor!.UserId;
            entry.GradedAt = now;
            entry.PublishedAt = null;
            entry.Passed = null;
            entry.SourceSubmissionId = submission.Id;
            entry.Version++;
            entry.UpdatedAt = now;
            session.Store(entry);
            representative ??= entry;
        }
        submission.Status = EssaySubmissionStatus.Graded;
        session.Store(submission);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(GradeEntryResponse.From(representative!));
    }

    private static async Task<IResult> PublishEssayGradeAsync(Guid gradeEntryId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var entry = await session.LoadAsync<GradeEntry>(gradeEntryId, cancellationToken);
        if (entry is null || entry.EssayAssignmentId is not { } assignmentId ||
            entry.EssayGroupId is not { } groupId)
        {
            return Results.NotFound();
        }

        var assignment = await session.LoadAsync<EssayAssignment>(assignmentId, cancellationToken);
        if (assignment is null || !await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (!entry.IsComplete)
        {
            return Results.Conflict(new { error = "The essay grade must be complete before publication." });
        }

        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        if (group is null)
        {
            return Results.NotFound();
        }

        var entries = await session.Query<GradeEntry>()
            .Where(value => value.EssayAssignmentId == assignmentId && value.EssayGroupId == groupId)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            entries = [entry];
        }

        if (entries.Any(value => !value.IsComplete))
        {
            return Results.Conflict(new { error = "Every group member grade must be complete before publication." });
        }

        var toPublish = entries.Where(value => value.Status != GradeEntryStatus.Published).ToArray();
        if (toPublish.Length > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var value in toPublish)
            {
                value.Status = GradeEntryStatus.Published;
                value.PublishedAt = now;
                value.UpdatedAt = now;
                value.Version++;
                session.Store(value);
            }
            await session.SaveChangesAsync(cancellationToken);
            var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.EssayGrading");
            foreach (var value in toPublish)
            {
                await GradePublicationPublisher.PublishAsync(messageBus, logger,
                    new GradeEntryPublished(value.OrganizationId, value.LearningClassId,
                        assignment.CourseId, value.StudentId, assignment.Id, value.GradeItemId, value.Id,
                        assignment.Title, value.AttemptNumber, value.Score, value.MaxScore, value.Version,
                        now));
            }
        }

        return Results.Ok(GradeEntryResponse.From(entries.Single(value => value.Id == gradeEntryId)));
    }

    private static async Task<IResult> GetEssayResultAsync(Guid assignmentId, Guid groupId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
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

        var canManage = await CanManageClassAsync(actor!, assignment.LearningClassId, academics,
            cancellationToken);
        if (!canManage && !await IsActiveGroupMemberAsync(actor!, group, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var studentId = canManage
            ? group.Members.OrderBy(member => member.JoinedAt).Select(member => member.StudentId)
                .FirstOrDefault()
            : actor!.UserId;
        var entry = studentId == Guid.Empty
            ? null
            : await session.LoadAsync<GradeEntry>(EssayConstants.GradeEntryId(assignmentId, groupId,
                studentId), cancellationToken);
        return entry is null || entry.Status != GradeEntryStatus.Published
            ? Results.NotFound()
            : Results.Ok(GradeEntryResponse.From(entry));
    }

    private static async Task<Guid> FindGroupClassAsync(IQuerySession session, Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await session.LoadAsync<EssayGroup>(groupId, cancellationToken);
        return group?.LearningClassId ?? Guid.Empty;
    }

    private static async Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return classId != Guid.Empty &&
            (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
             await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                 cancellationToken) ||
             await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken));
    }

    private static async Task<bool> IsActiveGroupMemberAsync(AuthenticatedActor actor,
        EssayGroup group, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return group.Members.Any(member => member.StudentId == actor.UserId) &&
            await academics.IsStudentEnrolledAsync(actor.OrganizationId, group.LearningClassId,
                actor.UserId, cancellationToken);
    }

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
            _ => (file.ContentType ?? "").Trim().ToLowerInvariant()
        };
    }
}

public sealed record CreateEssayAssignmentRequest(string Title, string? Description,
    DateTimeOffset? Deadline, int MaxScore, int MaxGroupSize, bool AllowResubmission,
    ReportValidationPolicy? ValidationPolicy);

public sealed record CreateEssayGroupRequest(string? Name);

public sealed record AddEssayGroupMemberRequest(Guid StudentId);

public sealed record RegisterEssayTopicRequest(string ProposedTitle);

public sealed record GradeEssaySubmissionRequest(decimal Score, string? Feedback);

public sealed record EssayAssignmentResponse(Guid Id, Guid LearningClassId, Guid? CourseId, string Title,
    string Description, DateTimeOffset? Deadline, int MaxScore, int MaxGroupSize,
    bool AllowResubmission, EssayAssignmentStatus Status, ReportValidationPolicy ValidationPolicy)
{
    public static EssayAssignmentResponse From(EssayAssignment assignment) => new(assignment.Id,
        assignment.LearningClassId, assignment.CourseId, assignment.Title, assignment.Description,
        assignment.Deadline, assignment.MaxScore, assignment.MaxGroupSize, assignment.AllowResubmission,
        assignment.Status, assignment.ValidationPolicy);
}

public sealed record EssayGroupResponse(Guid Id, Guid EssayAssignmentId, Guid LearningClassId,
    string Name, EssayGroupStatus Status, EssayGroupMember[] Members)
{
    public static EssayGroupResponse From(EssayGroup group) => new(group.Id, group.EssayAssignmentId,
        group.LearningClassId, group.Name, group.Status, group.Members.ToArray());
}

public sealed record EssayTopicResponse(Guid Id, Guid EssayAssignmentId, Guid EssayGroupId,
    string ProposedTitle, string? CanonicalTitle, EssayTopicStatus Status,
    DateTimeOffset SubmittedAt, Guid? ReviewedBy, DateTimeOffset? ReviewedAt)
{
    public static EssayTopicResponse From(EssayTopic topic) => new(topic.Id, topic.EssayAssignmentId,
        topic.EssayGroupId, topic.ProposedTitle, topic.CanonicalTitle, topic.Status, topic.SubmittedAt,
        topic.ReviewedBy, topic.ReviewedAt);
}

public sealed record EssaySubmissionResponse(Guid Id, Guid EssayAssignmentId, Guid EssayGroupId,
    Guid FileAssetId, int Version, EssaySubmissionStatus Status, DateTimeOffset UploadedAt,
    DateTimeOffset? SubmittedAt, string[] Errors)
{
    public static EssaySubmissionResponse From(EssaySubmission submission, string[] errors) => new(
        submission.Id, submission.EssayAssignmentId, submission.EssayGroupId, submission.FileAssetId,
        submission.Version, submission.Status, submission.UploadedAt, submission.SubmittedAt, errors);
}

public sealed record EssaySubmissionFileResponse(Guid SubmissionId, string FileName,
    string ContentType, long Size, string AccessUrl);
