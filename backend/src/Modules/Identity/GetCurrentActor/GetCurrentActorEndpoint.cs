using LearningPlatform.Identity.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LearningPlatform.Identity.GetCurrentActor;

public static class GetCurrentActorEndpoint
{
    public static void MapCurrentActor(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/identity/me", (HttpContext context) =>
        {
            return AuthenticatedActor.TryCreate(context.User, out var actor)
                ? Results.Ok(actor)
                : Results.Forbid();
        }).RequireAuthorization();
    }
}
