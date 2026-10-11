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

public sealed record ClassOverviewStudentResponse(Guid StudentId, Guid AttemptId,
    Guid? CurrentQuestionId, int AnsweredCount, int TotalQuestions, decimal Progress,
    StudentActivityStatus? Status, DateTimeOffset? LastActivityAt, bool HelpRequested, long Version,
    int AttemptNumber = 1)
{
    public string? StudentDisplayName { get; init; }

    public static ClassOverviewStudentResponse From(ClassOverviewStudent student) => new(student.StudentId,
        student.AttemptId, student.CurrentQuestionId, student.AnsweredCount, student.TotalQuestions,
        student.Progress, student.Status, student.LastActivityAt, student.HelpRequested, student.Version,
        student.AttemptNumber);

    public static ClassOverviewStudentResponse NotStarted(Guid studentId, int totalQuestions) =>
        new(studentId, Guid.Empty, null, 0, totalQuestions, 0m, null, null, false, 0);
}
