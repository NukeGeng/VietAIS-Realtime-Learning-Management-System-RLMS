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

internal static class AssessmentAccess
{


    internal static async Task<bool> CanManageClassAsync(
        AuthenticatedActor actor, Guid classId, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }



    internal static async Task<bool> CanReadStudentDetailAsync(
        AuthenticatedActor actor, Guid classId, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        // Organization administration can manage class metadata and reporting, but does not
        // implicitly grant access to live student answer detail.
        if (actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal))
        {
            return false;
        }

        return await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }



    internal static async Task<bool> CanAccessActivityAsync(
        AuthenticatedActor actor, AssessmentActivity activity, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (actor.Permissions.Contains(PermissionCatalog.ManageAssessmentActivities, StringComparer.Ordinal))
        {
            return await CanManageClassAsync(actor, activity.LearningClassId, academics, cancellationToken);
        }

        return activity.Status != AssessmentActivityStatus.Draft &&
            await CanStudentAccessAsync(actor, activity, academics, cancellationToken);
    }



    internal static async Task<bool> CanStudentAccessAsync(
        AuthenticatedActor actor, AssessmentActivity activity, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (activity.OrganizationId != actor.OrganizationId ||
            !actor.Permissions.Contains(PermissionCatalog.SubmitAssessmentActivities, StringComparer.Ordinal))
        {
            return false;
        }

        if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, activity.LearningClassId,
                actor.UserId, cancellationToken))
        {
            return false;
        }

        // A live activity remains readable after the session ends; its open/due rules still govern mutation.
        return true;
    }

}
