using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LearningPlatform.Assessment.Realtime;

public sealed class AssessmentRealtimeHub(
    IDocumentStore store,
    IAcademicDirectory academics) : Hub
{
    public async Task SubscribeOverview(Guid learningClassId, Guid activityId)
    {
        var actor = RequireActor();
        await using var session = store.QuerySession(actor.OrganizationId.ToString("D"));
        var activity = await session.LoadAsync<AssessmentActivity>(activityId);
        if (activity is null || activity.LearningClassId != learningClassId ||
            !await CanManageClassAsync(actor, learningClassId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId,
            ActivityRealtimeGroups.Overview(actor.OrganizationId, learningClassId, activityId));
    }

    public Task UnsubscribeOverview(Guid learningClassId, Guid activityId)
    {
        var actor = RequireActor();
        return Groups.RemoveFromGroupAsync(Context.ConnectionId,
            ActivityRealtimeGroups.Overview(actor.OrganizationId, learningClassId, activityId));
    }

    public async Task SubscribeStudentDetail(Guid attemptId)
    {
        var actor = RequireActor();
        await using var session = store.QuerySession(actor.OrganizationId.ToString("D"));
        var attempt = await session.LoadAsync<StudentActivity>(attemptId);
        if (attempt is null || attempt.OrganizationId != actor.OrganizationId ||
            !await CanManageClassAsync(actor, attempt.LearningClassId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId,
            ActivityRealtimeGroups.Detail(actor.OrganizationId, attemptId));
    }

    public Task UnsubscribeStudentDetail(Guid attemptId)
    {
        var actor = RequireActor();
        return Groups.RemoveFromGroupAsync(Context.ConnectionId,
            ActivityRealtimeGroups.Detail(actor.OrganizationId, attemptId));
    }

    private AuthenticatedActor RequireActor()
    {
        if (!AuthenticatedActor.TryCreate(Context.User!, out var actor))
        {
            Context.Abort();
            throw new HubException("Authentication is required.");
        }

        return actor!;
    }

    private async Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid learningClassId)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, learningClassId, actor.UserId);
    }
}
