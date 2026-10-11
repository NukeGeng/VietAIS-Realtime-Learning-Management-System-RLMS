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

namespace LearningPlatform.Assessment.Features.Assessment.GetOverview;

internal static class GetOverviewEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/assessment/classes/{classId:guid}/activities/{activityId:guid}/overview",
                GetOverviewAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAssessmentActivities);
    }


    private static async Task<IResult> GetOverviewAsync(
        Guid classId,
        Guid activityId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics, IIdentityDirectory identity,
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

        var activity = await session.LoadAsync<AssessmentActivity>(activityId, cancellationToken);
        if (activity is null || activity.LearningClassId != classId)
        {
            return Results.NotFound();
        }

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        var enrollments = await session.Query<Enrollment>()
            .Where(enrollment => enrollment.LearningClassId == classId &&
                enrollment.Status == EnrollmentStatus.Active)
            .OrderBy(enrollment => enrollment.StudentId)
            .ToListAsync(cancellationToken);
        var view = await session.LoadAsync<ClassOverviewView>(
            AssessmentConstants.ClassOverviewViewId(classId, activityId), cancellationToken);
        var summaries = (view?.Students ?? []).ToDictionary(student => student.StudentId);
        var names = await identity.FindMemberNamesAsync(actor.OrganizationId,
            enrollments.Select(enrollment => enrollment.StudentId).ToArray(), cancellationToken);
        var students = enrollments.Select(enrollment => summaries.TryGetValue(enrollment.StudentId,
                out var summary)
            ? ClassOverviewStudentResponse.From(summary)
            : ClassOverviewStudentResponse.NotStarted(enrollment.StudentId, activity.Questions.Count)).ToArray();
        students = students.Select(student => student with
        {
            StudentDisplayName = names.GetValueOrDefault(student.StudentId)
        }).ToArray();
        var questionProgress = activity.Questions.OrderBy(question => question.Order)
            .Select(question =>
            {
                var summary = view?.QuestionProgress?.FirstOrDefault(value =>
                    value.QuestionId == question.Id);
                return new ClassOverviewQuestionProgressResponse(question.Id, question.Order,
                    summary?.AnsweredStudentCount ?? 0, summary?.CurrentStudentCount ?? 0);
            }).ToArray();
        return Results.Ok(new ClassOverviewResponse(classId, activityId, view?.Version ?? 0,
            view?.UpdatedAt, students, questionProgress));
    }

}
