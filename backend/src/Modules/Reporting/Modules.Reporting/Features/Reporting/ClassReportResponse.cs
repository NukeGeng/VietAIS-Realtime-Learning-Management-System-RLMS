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

public sealed record ClassReportResponse(Guid LearningClassId, Guid CourseId, int EnrollmentCount,
    int ActiveEnrollmentCount, int CompletedEnrollmentCount, int ActivityCount,
    int PublishedActivityCount, int SubmissionCount, int PublishedGradeCount,
    decimal AverageScorePercent, int AttendanceSessionCount, int AttendancePresentCount,
    int AttendanceLateCount, int AttendanceAbsentCount, int AttendanceExcusedCount,
    decimal AttendancePercentage, int OverviewStudentCount, int OverviewInProgressCount,
    int OverviewSubmittedCount, int OverviewHelpRequestedCount, long Version,
    DateTimeOffset UpdatedAt)
{
    public static ClassReportResponse From(ClassReportSnapshot snapshot) => new(snapshot.LearningClassId,
        snapshot.CourseId, snapshot.EnrollmentCount, snapshot.ActiveEnrollmentCount,
        snapshot.CompletedEnrollmentCount, snapshot.ActivityCount, snapshot.PublishedActivityCount,
        snapshot.SubmissionCount, snapshot.PublishedGradeCount, snapshot.AverageScorePercent,
        snapshot.AttendanceSessionCount, snapshot.AttendancePresentCount, snapshot.AttendanceLateCount,
        snapshot.AttendanceAbsentCount, snapshot.AttendanceExcusedCount, snapshot.AttendancePercentage,
        snapshot.OverviewStudentCount, snapshot.OverviewInProgressCount, snapshot.OverviewSubmittedCount,
        snapshot.OverviewHelpRequestedCount, snapshot.Version, snapshot.UpdatedAt);

    public static ClassReportResponse Empty(LearningClass learningClass) => new(learningClass.Id,
        learningClass.CourseId, 0, 0, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0, 0, 0m, 0, 0, 0, 0, 0,
        DateTimeOffset.UtcNow);
}
