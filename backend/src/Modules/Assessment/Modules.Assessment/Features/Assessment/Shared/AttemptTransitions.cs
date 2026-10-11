using LearningPlatform.Assessment.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Contracts;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Assessment.Realtime;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Assessment.Features.Assessment.Shared.ActivityPublication;
using static LearningPlatform.Assessment.Features.Assessment.Shared.ActivityNotifications;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptTransitions;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AssessmentAccess;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptAvailability;
using static LearningPlatform.Assessment.Features.Assessment.Shared.AttemptSnapshots;
using static LearningPlatform.Assessment.Features.Assessment.Shared.QuestionValidation;

namespace LearningPlatform.Assessment.Features.Assessment.Shared;

internal static class AttemptTransitions
{


    internal static async Task<IResult> ChangeAttemptStateAsync(Guid activityId,
        AttemptStateChange change, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, IMessageBus messageBus, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanStudentAccessAsync(actor!, activity, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var currentAttempt = await FindLatestAttemptAsync(session, activityId, actor!.UserId,
            cancellationToken);
        if (currentAttempt is null)
        {
            return Results.Conflict(new { error = "Start the activity before changing its state." });
        }

        var stream = await session.Events.FetchForWriting<StudentActivity>(currentAttempt.Id,
            cancellationToken);
        var aggregate = stream.Aggregate;
        if (aggregate is null)
        {
            return Results.Conflict(new { error = "Start the activity before changing its state." });
        }

        if (aggregate.Status is StudentActivityStatus.Submitted or StudentActivityStatus.Closed)
        {
            return Results.Conflict(new { error = "This activity attempt is already finished." });
        }

        var now = DateTimeOffset.UtcNow;
        if (!IsAttemptOpen(activity, aggregate, now))
        {
            return Results.Conflict(new { error = "This activity attempt is no longer open." });
        }

        object @event;
        string changeType;
        switch (change)
        {
            case AttemptStateChange.Idle when aggregate.Status == StudentActivityStatus.InProgress:
                @event = new StudentBecameIdle(aggregate.Id, now);
                changeType = ActivityChangeTypes.Idle;
                break;
            case AttemptStateChange.Resume when aggregate.Status is StudentActivityStatus.Idle or
                StudentActivityStatus.Paused:
                @event = new StudentActivityResumed(aggregate.Id, now);
                changeType = ActivityChangeTypes.Resumed;
                break;
            case AttemptStateChange.HelpRequested when !aggregate.HelpRequested:
                @event = new ActivityHelpRequested(aggregate.Id, now);
                changeType = ActivityChangeTypes.HelpRequested;
                break;
            case AttemptStateChange.HelpWithdrawn when aggregate.HelpRequested:
                @event = new ActivityHelpResolved(aggregate.Id, now);
                changeType = ActivityChangeTypes.HelpResolved;
                break;
            default:
                return Results.Conflict(new { error = "The activity attempt cannot make that state transition." });
        }

        var nextState = Clone(aggregate);
        ApplyAttemptEvent(nextState, @event);
        switch (@event)
        {
            case ActivityHelpRequested:
                session.Insert(new HelpRequest
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = aggregate.OrganizationId,
                    LiveSessionId = aggregate.LiveSessionId,
                    StudentActivityId = aggregate.Id,
                    ActivityId = aggregate.ActivityId,
                    LearningClassId = aggregate.LearningClassId,
                    StudentId = aggregate.StudentId,
                    QuestionId = aggregate.CurrentQuestionId,
                    Status = HelpRequestStatus.Requested,
                    RequestedAt = now
                });
                break;
            case ActivityHelpResolved:
                var activeRequest = await FindActiveHelpRequestAsync(session, aggregate.Id,
                    cancellationToken);
                if (activeRequest is not null)
                {
                    activeRequest.Status = HelpRequestStatus.Resolved;
                    activeRequest.ResolvedAt = now;
                    activeRequest.HandledBy = actor.UserId;
                    session.Update(activeRequest);
                }
                break;
        }
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);
        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor.OrganizationId, aggregate.Id, activity.Id, actor.UserId,
                activity.LearningClassId, changeType, null, null,
                StudentActivityRealtimeStateFactory.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(nextState, activity));
    }



    internal static async Task<IResult> ChangeAttemptHelpAsync(Guid attemptId, bool resolve,
        HttpContext context, IDocumentSession session, IAcademicDirectory academics,
        IMessageBus messageBus, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var stream = await session.Events.FetchForWriting<StudentActivity>(attemptId,
            cancellationToken);
        var aggregate = stream.Aggregate;
        if (aggregate is null)
        {
            return Results.NotFound();
        }

        var activity = await session.LoadAsync<AssessmentActivity>(aggregate.ActivityId,
            cancellationToken);
        if (activity is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor!, activity.LearningClassId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!aggregate.HelpRequested)
        {
            return Results.Conflict(new { error = "This attempt has no active help request." });
        }

        var now = DateTimeOffset.UtcNow;
        var activeRequest = await FindActiveHelpRequestAsync(session, attemptId, cancellationToken);
        object @event = resolve
            ? new ActivityHelpResolved(attemptId, now)
            : new ActivityHelpAcknowledged(attemptId, now);
        var changeType = resolve ? ActivityChangeTypes.HelpResolved : ActivityChangeTypes.HelpAcknowledged;
        var nextState = Clone(aggregate);
        ApplyAttemptEvent(nextState, @event);
        if (activeRequest is not null)
        {
            if (resolve)
            {
                activeRequest.Status = HelpRequestStatus.Resolved;
                activeRequest.ResolvedAt = now;
            }
            else if (activeRequest.Status == HelpRequestStatus.Requested)
            {
                activeRequest.Status = HelpRequestStatus.Acknowledged;
                activeRequest.AcknowledgedAt = now;
            }

            activeRequest.HandledBy = actor!.UserId;
            session.Update(activeRequest);
        }
        stream.AppendOne(@event);
        await session.SaveChangesAsync(cancellationToken);
        var logger = loggerFactory.CreateLogger("LearningPlatform.Assessment.Api");
        await StudentActivityChangePublisher.PublishAsync(messageBus, logger,
            new StudentActivityChanged(actor!.OrganizationId, attemptId, activity.Id, aggregate.StudentId,
                activity.LearningClassId, changeType, null, null,
                StudentActivityRealtimeStateFactory.From(nextState), now));
        return Results.Ok(StudentActivityResponse.From(nextState, activity));
    }



    internal static Task<HelpRequest?> FindActiveHelpRequestAsync(IDocumentSession session,
        Guid attemptId, CancellationToken cancellationToken) => session.Query<HelpRequest>()
        .Where(request => request.StudentActivityId == attemptId &&
            request.Status != HelpRequestStatus.Resolved)
        .OrderByDescending(request => request.RequestedAt)
        .FirstOrDefaultAsync(cancellationToken);



    internal static void ApplyAttemptEvent(StudentActivity state, object @event)
    {
        switch (@event)
        {
            case StudentBecameIdle idle:
                state.Apply(idle);
                break;
            case StudentActivityResumed resumed:
                state.Apply(resumed);
                break;
            case ActivityHelpRequested requested:
                state.Apply(requested);
                break;
            case ActivityHelpAcknowledged acknowledged:
                state.Apply(acknowledged);
                break;
            case ActivityHelpResolved resolved:
                state.Apply(resolved);
                break;
            default:
                throw new InvalidOperationException($"Unsupported student activity event {@event.GetType().Name}.");
        }
    }



    internal enum AttemptStateChange
    {
        Idle,
        Resume,
        HelpRequested,
        HelpWithdrawn
    }

}
