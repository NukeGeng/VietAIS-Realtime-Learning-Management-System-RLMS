using LearningPlatform.Classroom.Api;
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
using static LearningPlatform.Classroom.Api.AttendanceEndpoints;

namespace LearningPlatform.Classroom.Features.Attendance.ListAttendance;

internal static class ListAttendanceEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/classroom/sessions/{sessionId:guid}/attendance", ListAttendanceAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAttendance);
    }


    private static async Task<IResult> ListAttendanceAsync(
        Guid sessionId,
        HttpContext context,
        IQuerySession session,
        IAcademicDirectory academics,
        IIdentityDirectory identity,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var liveSession = await session.LoadAsync<LiveSession>(sessionId, cancellationToken);
        if (liveSession is null)
        {
            return Results.NotFound();
        }

        if (liveSession.OrganizationId != actor!.OrganizationId)
        {
            return Results.Forbid();
        }

        if (!await LiveSessionAuthorization.CanManageAttendanceAsync(actor, liveSession, academics,
                cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await session.Query<AttendanceRecord>()
            .Where(record => record.OrganizationId == actor.OrganizationId &&
                record.LiveSessionId == sessionId)
            .OrderBy(record => record.StudentId)
            .ToListAsync(cancellationToken);
        var totalStudents = await session.Query<Enrollment>()
            .CountAsync(enrollment => enrollment.OrganizationId == actor.OrganizationId &&
                enrollment.LearningClassId == liveSession.LearningClassId &&
                enrollment.Status == EnrollmentStatus.Active, cancellationToken);
        var names = await identity.FindMemberNamesAsync(actor.OrganizationId,
            records.Select(record => record.StudentId).ToArray(), cancellationToken);
        var response = AttendanceSummaryResponse.From(records, totalStudents);
        return Results.Ok(response with
        {
            Records = response.Records.Select(record => record with
            {
                StudentDisplayName = names.GetValueOrDefault(record.StudentId)
            }).ToArray()
        });
    }

}
