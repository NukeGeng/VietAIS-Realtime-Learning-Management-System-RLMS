using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Classroom.Api;

public sealed record AttendanceSummaryResponse(
    int TotalStudents,
    int PresentCount,
    int LateCount,
    int AbsentCount,
    int ExcusedCount,
    double AttendancePercentage,
    IReadOnlyList<AttendanceRecordResponse> Records)
{
    public static AttendanceSummaryResponse From(IReadOnlyCollection<AttendanceRecord> records,
        int totalStudents)
    {
        var present = records.Count(record => record.Status == AttendanceStatus.Present);
        var late = records.Count(record => record.Status == AttendanceStatus.Late);
        var absent = records.Count(record => record.Status == AttendanceStatus.Absent);
        var excused = records.Count(record => record.Status == AttendanceStatus.Excused);
        var attended = present + late + excused;
        var percentage = totalStudents == 0 ? 0 : Math.Round(attended * 100d / totalStudents, 2);
        return new AttendanceSummaryResponse(totalStudents, present, late, absent, excused, percentage,
            records.Select(AttendanceRecordResponse.From).ToArray());
    }
}
