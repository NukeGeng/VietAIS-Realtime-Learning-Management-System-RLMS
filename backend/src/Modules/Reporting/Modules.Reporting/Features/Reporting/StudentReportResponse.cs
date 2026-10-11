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

public sealed record StudentReportResponse(Guid LearningClassId, Guid StudentId, int PublishedGradeCount,
    decimal AverageScorePercent, int CompletedActivityCount, int ActivityCount,
    decimal ActivityCompletionPercent, int LessonCount, int CompletedLessonCount,
    decimal LessonCompletionPercent, int AttendanceSessionCount, int AttendancePresentCount,
    int AttendanceLateCount, int AttendanceAbsentCount, int AttendanceExcusedCount,
    decimal AttendancePercentage, long Version, DateTimeOffset UpdatedAt)
{
    public static StudentReportResponse From(StudentReportSnapshot snapshot) => new(snapshot.LearningClassId,
        snapshot.StudentId, snapshot.PublishedGradeCount, snapshot.AverageScorePercent,
        snapshot.CompletedActivityCount, snapshot.ActivityCount, snapshot.ActivityCompletionPercent,
        snapshot.LessonCount, snapshot.CompletedLessonCount, snapshot.LessonCompletionPercent,
        snapshot.AttendanceSessionCount, snapshot.AttendancePresentCount, snapshot.AttendanceLateCount,
        snapshot.AttendanceAbsentCount, snapshot.AttendanceExcusedCount, snapshot.AttendancePercentage,
        snapshot.Version, snapshot.UpdatedAt);

    public static StudentReportResponse Empty(Guid classId, Guid studentId) => new(classId,
        studentId, 0, 0m, 0, 0, 0m, 0, 0, 0m, 0, 0, 0, 0, 0, 0m, 0,
        DateTimeOffset.UtcNow);
}
