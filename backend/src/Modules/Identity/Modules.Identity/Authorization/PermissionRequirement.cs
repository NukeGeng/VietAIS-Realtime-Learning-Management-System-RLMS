using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.Identity.Authorization;


public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (AuthenticatedActor.TryCreate(context.User, out var actor) &&
            actor!.Permissions.Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
