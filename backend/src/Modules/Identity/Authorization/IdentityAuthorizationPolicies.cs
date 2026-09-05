using LearningPlatform.Identity.Domain;
using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.Identity.Authorization;

public static class IdentityAuthorizationPolicies
{
    public const string ManageUsers = "identity-users-manage";

    public static void AddIdentityPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(ManageUsers, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(PermissionCatalog.ManageUsers)));
    }
}
