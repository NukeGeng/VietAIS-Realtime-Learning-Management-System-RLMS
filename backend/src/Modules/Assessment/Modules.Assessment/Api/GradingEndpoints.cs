using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
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

public static class GradingEndpoints
{
    public static void MapGradingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Assessment.Features.Grading.GetRubric.GetRubricEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.CreateRubric.CreateRubricEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.GetGradeEntry.GetGradeEntryEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.GradeSubmission.GradeSubmissionEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.PublishGradeEntry.PublishGradeEntryEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.UpdateGradeItem.UpdateGradeItemEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.AdjustGradeEntry.AdjustGradeEntryEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.ListGradeAdjustments.ListGradeAdjustmentsEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.GetStudentResult.GetStudentResultEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.GetTeacherGradebook.GetTeacherGradebookEndpoint.Map(endpoints);
        LearningPlatform.Assessment.Features.Grading.GetStudentGradebook.GetStudentGradebookEndpoint.Map(endpoints);
    }

    internal static async Task<IResult> GetGradebookAsync(Guid classId, Guid? studentFilter,
        HttpContext context, IQuerySession session, IAcademicDirectory academics, IIdentityDirectory identity,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (await academics.FindClassAsync(actor!.OrganizationId, classId, cancellationToken) is null)
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
        else if (!await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId, actor.UserId,
                     cancellationToken))
        {
            return Results.Forbid();
        }

        var activities = await session.Query<AssessmentActivity>()
            .Where(activity => activity.LearningClassId == classId &&
                activity.Status != AssessmentActivityStatus.Draft)
            .OrderBy(activity => activity.CreatedAt)
            .ToListAsync(cancellationToken);
        var activityIds = activities.Select(activity => activity.Id).ToArray();
        var essays = await session.Query<EssayAssignment>()
            .Where(assignment => assignment.LearningClassId == classId &&
                assignment.Status != EssayAssignmentStatus.Draft)
            .OrderBy(assignment => assignment.CreatedAt)
            .ToListAsync(cancellationToken);
        var essayAssignmentIds = essays.Select(assignment => assignment.Id).ToArray();
        var items = await session.Query<GradeItem>()
            .Where(item => item.LearningClassId == classId &&
                (activityIds.Contains(item.ActivityId) ||
                 item.EssayAssignmentId != null && essayAssignmentIds.Contains(item.EssayAssignmentId.Value)))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var entries = await session.Query<GradeEntry>()
            .Where(entry => entry.LearningClassId == classId &&
                (activityIds.Contains(entry.ActivityId) ||
                 entry.EssayAssignmentId != null && essayAssignmentIds.Contains(entry.EssayAssignmentId.Value)) &&
                (studentFilter == null || entry.StudentId == actor.UserId))
            .ToListAsync(cancellationToken);
        if (!isTeacher)
        {
            var activityById = activities.ToDictionary(activity => activity.Id);
            entries = entries.Where(entry => !activityById.TryGetValue(entry.ActivityId,
                    out var activity) || AssessmentRules.IsResultsReleased(activity,
                    DateTimeOffset.UtcNow))
                .ToList();
        }
        var itemResponses = items.Select(GradebookItemResponse.From).ToArray();
        Guid[] studentIds;
        if (studentFilter is null)
        {
            studentIds = (await session.Query<Enrollment>()
                .Where(enrollment => enrollment.LearningClassId == classId &&
                    enrollment.Status == EnrollmentStatus.Active)
                .OrderBy(enrollment => enrollment.StudentId)
                .Select(enrollment => enrollment.StudentId)
                .ToListAsync(cancellationToken)).ToArray();
        }
        else
        {
            studentIds = [actor.UserId];
        }

        var names = await identity.FindMemberNamesAsync(actor.OrganizationId, studentIds,
            cancellationToken);
        var rows = studentIds.Select(studentId =>
        {
            var cells = items.Select(item =>
            {
                var entry = entries.Where(value => value.GradeItemId == item.Id &&
                        value.StudentId == studentId && (isTeacher || value.Status == GradeEntryStatus.Published))
                    .OrderByDescending(value => value.AttemptNumber)
                    .FirstOrDefault();
                return GradebookCellResponse.From(item, entry);
            }).ToArray();
            var visible = cells.Where(cell => cell.Score is not null).ToArray();
            var visibleMaxScore = visible.Sum(cell => cell.MaxScore);
            var average = visible.Length == 0 || visibleMaxScore == 0m
                ? (decimal?)null
                : visible.Sum(cell => cell.Score!.Value) / visibleMaxScore;
            var weightTotal = visible.Sum(cell => cell.Weight);
            var weightedAverage = weightTotal <= 0m
                ? (decimal?)null
                : visible.Sum(cell => cell.MaxScore == 0m
                    ? 0m
                    : cell.Score!.Value / cell.MaxScore * cell.Weight) / weightTotal;
            decimal? finalScore = weightedAverage is { } weighted
                ? decimal.Round(weighted * 100m, 2)
                : null;
            return new GradebookStudentRowResponse(studentId, average, weightedAverage, finalScore, cells)
            {
                StudentDisplayName = names.GetValueOrDefault(studentId)
            };
        }).ToArray();
        return Results.Ok(new GradebookResponse(classId, itemResponses, rows));
    }


    internal static async Task<bool> CanManageClassAsync(AuthenticatedActor actor, Guid classId,
        IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        return actor.Permissions.Contains(PermissionCatalog.ManageOrganizations, StringComparer.Ordinal) ||
            await academics.IsClassCreatorAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken) ||
            await academics.IsTeacherAssignedAsync(actor.OrganizationId, classId, actor.UserId,
                cancellationToken);
    }

}
