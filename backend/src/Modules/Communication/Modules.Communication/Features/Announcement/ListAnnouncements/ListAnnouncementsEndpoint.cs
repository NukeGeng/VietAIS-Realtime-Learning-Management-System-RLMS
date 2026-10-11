using LearningPlatform.Communication.Api;
using LearningPlatform.Academics.Authorization;
using LearningPlatform.Academics.Domain;
using LearningPlatform.Communication.Domain;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Wolverine;
using static LearningPlatform.Communication.Api.AnnouncementEndpoints;

namespace LearningPlatform.Communication.Features.Announcements.ListAnnouncements;

internal static class ListAnnouncementsEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/communication/classes/{classId:guid}/announcements",
                ListAnnouncementsAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAnnouncements);
    }


    private static async Task<IResult> ListAnnouncementsAsync(Guid classId, HttpContext context,
        IQuerySession session, IAcademicDirectory academics, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var learningClass = await academics.FindClassAsync(actor!.OrganizationId, classId,
            cancellationToken);
        if (learningClass is null)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor, classId, academics, cancellationToken);
        if (!canManage && !await academics.IsStudentEnrolledAsync(actor.OrganizationId, classId,
                actor.UserId, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = session.Query<Announcement>()
            .Where(announcement => announcement.OrganizationId == actor.OrganizationId &&
                announcement.LearningClassId == classId);
        if (!canManage)
        {
            query = query.Where(announcement => announcement.Status == AnnouncementStatus.Published);
        }

        var announcements = await query.OrderByDescending(announcement => announcement.PublishedAt)
            .ThenByDescending(announcement => announcement.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return Results.Ok(announcements.Select(AnnouncementResponse.From));
    }

}
