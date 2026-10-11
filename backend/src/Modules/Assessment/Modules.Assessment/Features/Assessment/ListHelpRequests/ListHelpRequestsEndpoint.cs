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

namespace LearningPlatform.Assessment.Features.Assessment.ListHelpRequests;

internal static class ListHelpRequestsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/help-requests",
                ListHelpRequestsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> ListHelpRequestsAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, IIdentityDirectory identity, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var requests = await session.Query<HelpRequest>()
            .Where(request => request.LearningClassId == classId &&
                request.Status != HelpRequestStatus.Resolved)
            .OrderBy(request => request.RequestedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        if (requests.Count == 0)
        {
            return Results.Ok(Array.Empty<HelpRequestResponse>());
        }

        var attemptIds = requests.Select(request => request.StudentActivityId).Distinct().ToArray();
        var attempts = (await session.Query<StudentActivity>()
            .Where(attempt => attemptIds.Contains(attempt.Id))
            .ToListAsync(cancellationToken))
            .ToDictionary(attempt => attempt.Id);
        var activityIds = requests.Select(request => request.ActivityId).Distinct().ToArray();
        var activities = (await session.Query<AssessmentActivity>()
            .Where(activity => activityIds.Contains(activity.Id))
            .ToListAsync(cancellationToken))
            .ToDictionary(activity => activity.Id);
        var names = await identity.FindMemberNamesAsync(actor.OrganizationId,
            requests.Select(request => request.StudentId).ToArray(), cancellationToken);
        return Results.Ok(requests
            .Where(request => attempts.ContainsKey(request.StudentActivityId) &&
                activities.ContainsKey(request.ActivityId))
            .Select(request => HelpRequestResponse.From(request,
                attempts[request.StudentActivityId], activities[request.ActivityId]) with
            {
                StudentDisplayName = names.GetValueOrDefault(request.StudentId)
            })
            .ToArray());
    }

}
