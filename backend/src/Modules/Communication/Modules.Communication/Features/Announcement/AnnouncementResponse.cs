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

namespace LearningPlatform.Communication.Api;

public sealed record AnnouncementResponse(Guid Id, Guid LearningClassId, Guid AuthorId, string Title,
    string Content, AnnouncementStatus Status, DateTimeOffset? PublishedAt, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static AnnouncementResponse From(Announcement announcement) => new(announcement.Id,
        announcement.LearningClassId, announcement.AuthorId, announcement.Title, announcement.Content,
        announcement.Status, announcement.PublishedAt, announcement.CreatedAt, announcement.UpdatedAt);
}
