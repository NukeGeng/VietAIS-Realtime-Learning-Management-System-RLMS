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

namespace LearningPlatform.Assessment.Api;

public sealed record SubmissionResponse(Guid Id, Guid ActivityId, Guid StudentId,
    Guid StudentActivityId, int AttemptNumber, DateTimeOffset SubmittedAt, SubmissionStatus Status,
    int Version, SubmissionAnswer[] Answers)
{
    public static SubmissionResponse From(Submission submission) => new(submission.Id, submission.ActivityId,
        submission.StudentId, submission.StudentActivityId, submission.AttemptNumber,
        submission.SubmittedAt, submission.Status, submission.Version, submission.Answers);
}
