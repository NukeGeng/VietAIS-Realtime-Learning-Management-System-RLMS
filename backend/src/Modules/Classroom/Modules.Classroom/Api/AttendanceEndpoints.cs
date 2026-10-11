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

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Classroom.Features.Attendance.ListAttendance.ListAttendanceEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.Attendance.AdjustAttendance.AdjustAttendanceEndpoint.Map(endpoints);
    }

}
