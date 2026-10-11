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

namespace LearningPlatform.Communication.Features.Announcements.ArchiveAnnouncement;

internal static class ArchiveAnnouncementEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/communication/announcements/{announcementId:guid}/archive",
                ArchiveAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAnnouncements);
    }


    private static Task<IResult> ArchiveAnnouncementAsync(Guid announcementId, HttpContext context,
        IDocumentSession session, IAcademicDirectory academics, IMessageBus messageBus,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        ChangeAnnouncementStatusAsync(announcementId, AnnouncementStatus.Archived, context, session,
            academics, messageBus, loggerFactory, cancellationToken);

}
