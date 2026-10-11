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

internal static class AttemptSnapshots
{


    internal static StudentActivity Clone(StudentActivity source) => new()
    {
        Id = source.Id,
        OrganizationId = source.OrganizationId,
        StudentId = source.StudentId,
        ActivityId = source.ActivityId,
        LearningClassId = source.LearningClassId,
        LiveSessionId = source.LiveSessionId,
        CurrentQuestionId = source.CurrentQuestionId,
        Status = source.Status,
        Answers = new Dictionary<Guid, string>(source.Answers),
        QuestionOrder = source.QuestionOrder.ToArray(),
        HelpRequested = source.HelpRequested,
        HelpRequestedAt = source.HelpRequestedAt,
        HelpAcknowledgedAt = source.HelpAcknowledgedAt,
        AnsweredCount = source.AnsweredCount,
        StartedAt = source.StartedAt,
        LastActivityAt = source.LastActivityAt,
        SubmittedAt = source.SubmittedAt,
        Version = source.Version,
        AttemptNumber = source.AttemptNumber
    };



    internal static StudentAttemptDetailView StudentAttemptDetailViewFrom(StudentActivity activity,
        int totalQuestions) => new()
        {
            Id = activity.Id,
            OrganizationId = activity.OrganizationId,
            StudentId = activity.StudentId,
            AttemptNumber = activity.AttemptNumber,
            ActivityId = activity.ActivityId,
            LearningClassId = activity.LearningClassId,
            LiveSessionId = activity.LiveSessionId,
            CurrentQuestionId = activity.CurrentQuestionId,
            QuestionOrder = activity.QuestionOrder.ToArray(),
            Status = activity.Status,
            Answers = new Dictionary<Guid, string>(activity.Answers),
            HelpRequested = activity.HelpRequested,
            HelpRequestedAt = activity.HelpRequestedAt,
            HelpAcknowledgedAt = activity.HelpAcknowledgedAt,
            AnsweredCount = activity.AnsweredCount,
            TotalQuestions = totalQuestions,
            Progress = totalQuestions == 0 ? 0m : decimal.Round(activity.AnsweredCount * 100m / totalQuestions, 2),
            StartedAt = activity.StartedAt,
            LastActivityAt = activity.LastActivityAt,
            SubmittedAt = activity.SubmittedAt,
            Version = activity.Version
        };

}
