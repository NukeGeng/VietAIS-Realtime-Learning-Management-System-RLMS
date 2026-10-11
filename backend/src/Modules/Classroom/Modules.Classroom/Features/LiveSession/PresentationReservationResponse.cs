using System.Security.Cryptography;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace LearningPlatform.Classroom.Api;

public sealed record PresentationReservationResponse(
    bool Acquired,
    Guid? PresenterId,
    bool PresenterIsTeacher)
{
    public static PresentationReservationResponse Busy(LivePresentationLease? lease) =>
        new(false, lease?.UserId, lease?.IsTeacher ?? false);

    public static PresentationReservationResponse AcquiredBy(LivePresentationLease lease) =>
        new(true, lease.UserId, lease.IsTeacher);
}
