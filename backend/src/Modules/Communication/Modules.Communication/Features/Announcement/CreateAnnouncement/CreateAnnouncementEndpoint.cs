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

namespace LearningPlatform.Communication.Features.Announcements.CreateAnnouncement;

internal static class CreateAnnouncementEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/communication/classes/{classId:guid}/announcements",
                CreateAnnouncementAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageAnnouncements);
    }


    private static async Task<IResult> CreateAnnouncementAsync(Guid classId,
        CreateAnnouncementRequest request, HttpContext context, IDocumentSession session,
        IAcademicDirectory academics, CancellationToken cancellationToken)
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

        if (!await CanManageClassAsync(actor, classId, academics, cancellationToken))
        {
            return Results.Forbid();
        }

        if (learningClass.Status is LearningClassStatus.Completed or LearningClassStatus.Archived)
        {
            return Results.Conflict(new { error = "A completed or archived class cannot receive a new announcement." });
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var announcement = new Announcement
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor.OrganizationId,
                LearningClassId = classId,
                AuthorId = actor.UserId,
                Title = CommunicationRules.RequireAnnouncementTitle(request.Title),
                Content = CommunicationRules.RequireAnnouncementContent(request.Content),
                Status = AnnouncementStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(announcement);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/communication/announcements/{announcement.Id:D}",
                AnnouncementResponse.From(announcement));
        }
        catch (CommunicationValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
