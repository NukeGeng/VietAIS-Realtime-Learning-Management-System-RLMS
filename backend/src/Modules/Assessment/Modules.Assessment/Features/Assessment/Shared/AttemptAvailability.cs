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

internal static class AttemptAvailability
{


    internal static bool IsOpen(AssessmentActivity activity, DateTimeOffset now)
    {
        return activity.Status == AssessmentActivityStatus.Published &&
            (activity.OpensAt is null || now >= activity.OpensAt) &&
            (activity.DueAt is null || now <= activity.DueAt);
    }



    internal static bool IsAttemptOpen(AssessmentActivity activity, StudentActivity attempt,
        DateTimeOffset now)
    {
        return IsOpen(activity, now) &&
            (activity.TimeLimit is not { } limit || now <= attempt.StartedAt + limit);
    }



    internal static async Task<StudentActivity?> FindLatestAttemptAsync(IQuerySession session,
        Guid activityId, Guid studentId, CancellationToken cancellationToken)
    {
        var attempts = await session.Query<StudentActivity>()
            .Where(attempt => attempt.ActivityId == activityId && attempt.StudentId == studentId)
            .OrderByDescending(attempt => attempt.AttemptNumber)
            .ToListAsync(cancellationToken);
        return attempts.FirstOrDefault();
    }



    internal static bool IsExamRetakeAllowed(AssessmentActivity activity, int attemptNumber)
    {
        return activity.Kind == AssessmentActivityKind.Exam && activity.ExamPolicy is
        { AllowRetake: true } policy && attemptNumber < policy.AttemptLimit;
    }

}
