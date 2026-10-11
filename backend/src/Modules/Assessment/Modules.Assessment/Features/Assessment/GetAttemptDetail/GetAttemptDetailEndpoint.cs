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

namespace LearningPlatform.Assessment.Features.Assessment.GetAttemptDetail;

internal static class GetAttemptDetailEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/attempts/{attemptId:guid}/detail", GetAttemptDetailAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> GetAttemptDetailAsync(
        Guid attemptId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics, IIdentityDirectory identity,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var detail = await session.LoadAsync<StudentAttemptDetailView>(attemptId, cancellationToken);
        var attempt = await session.LoadAsync<StudentActivity>(attemptId, cancellationToken);
        if (attempt is null)
        {
            return Results.NotFound();
        }

        // StudentAttemptDetailView is an asynchronous read model. A teacher
        // opening or reconnecting to detail must not be handed an older
        // snapshot while the durable StudentActivity aggregate is already
        // newer. Use the aggregate as the read-repair source for this
        // on-demand query; the next projection message will persist the view.
        if (detail is null || detail.Version < attempt.Version)
        {
            var activity = await session.LoadAsync<AssessmentActivity>(attempt.ActivityId, cancellationToken);
            if (activity is null)
            {
                return Results.NotFound();
            }

            detail = StudentAttemptDetailViewFrom(attempt, activity.Questions.Count);
        }

        if (attempt.OrganizationId != actor!.OrganizationId ||
            !await CanReadStudentDetailAsync(actor, attempt.LearningClassId, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var names = await identity.FindMemberNamesAsync(actor.OrganizationId, [detail.StudentId],
            cancellationToken);
        return Results.Ok(StudentAttemptDetailResponse.From(detail) with
        {
            StudentDisplayName = names.GetValueOrDefault(detail.StudentId)
        });
    }

}
