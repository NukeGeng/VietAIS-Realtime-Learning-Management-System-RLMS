using LearningPlatform.Academics.Authorization;
using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using LearningPlatform.Identity.Authorization;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LearningPlatform.Classroom.Api;

public static class LiveSessionModerationEndpoints
{
    public static void MapLiveSessionModerationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Classroom.Features.LiveSessionModeration.List.ListEndpoint.Map(endpoints);
        LearningPlatform.Classroom.Features.LiveSessionModeration.Update.UpdateEndpoint.Map(endpoints);
    }

}
