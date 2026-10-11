using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Assessment.Domain;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using LearningPlatform.Learning.Domain;
using LearningPlatform.Reporting.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LearningPlatform.Reporting.Api;

public sealed record ActivityReportResponse(Guid ActivityId, Guid LearningClassId, string Title,
    int EnrolledStudentCount, int SubmissionCount, int PublishedGradeCount,
    QuestionReportResponse[] Questions);
