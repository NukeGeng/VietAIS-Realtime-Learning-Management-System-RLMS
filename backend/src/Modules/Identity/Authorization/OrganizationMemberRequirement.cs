using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.Identity.Authorization;

public sealed class OrganizationMemberRequirement : IAuthorizationRequirement;

public sealed class OrganizationMemberHandler : AuthorizationHandler<OrganizationMemberRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrganizationMemberRequirement requirement)
    {
        if (AuthenticatedActor.TryCreate(context.User, out _))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
