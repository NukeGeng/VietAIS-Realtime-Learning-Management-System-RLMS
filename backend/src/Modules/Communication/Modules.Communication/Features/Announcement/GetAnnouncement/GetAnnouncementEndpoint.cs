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

namespace LearningPlatform.Communication.Features.Announcements.GetAnnouncement;

internal static class GetAnnouncementEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/communication/announcements/{announcementId:guid}",
                GetAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadAnnouncements);
    }


    private static async Task<IResult> GetAnnouncementAsync(Guid announcementId,
        HttpContext context, IQuerySession session, IAcademicDirectory academics,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var announcement = await session.LoadAsync<Announcement>(announcementId, cancellationToken);
        if (announcement is null || announcement.OrganizationId != actor!.OrganizationId)
        {
            return Results.NotFound();
        }

        var canManage = await CanManageClassAsync(actor!, announcement.LearningClassId, academics,
            cancellationToken);
        if (!canManage && (announcement.Status != AnnouncementStatus.Published ||
                !await academics.IsStudentEnrolledAsync(actor!.OrganizationId,
                    announcement.LearningClassId, actor.UserId, cancellationToken)))
        {
            return Results.Forbid();
        }

        return Results.Ok(AnnouncementResponse.From(announcement));
    }

}
