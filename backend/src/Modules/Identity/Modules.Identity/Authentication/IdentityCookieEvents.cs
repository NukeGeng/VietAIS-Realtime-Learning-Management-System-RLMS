using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LearningPlatform.Identity.Authentication;

public sealed class IdentityCookieEvents : CookieAuthenticationEvents
{
    private readonly IDocumentStore store;
    private readonly ILogger<IdentityCookieEvents> logger;

    public IdentityCookieEvents(IDocumentStore store, ILogger<IdentityCookieEvents> logger)
    {
        this.store = store;
        this.logger = logger;
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(AuthenticatedActor.OrganizationClaim), out var organizationId) ||
            string.IsNullOrWhiteSpace(principal.FindFirstValue(IdentityClaims.SecurityStampClaim)))
        {
            context.RejectPrincipal();
            return;
        }

        try
        {
            await using var control = store.QuerySession(IdentityConstants.ControlPlaneTenantId);
            var organization = await control.LoadAsync<Organization>(organizationId);
            if (organization is null || organization.Status != OrganizationStatus.Active)
            {
                context.RejectPrincipal();
                return;
            }

            await using var session = store.QuerySession(organizationId.ToString("D"));
            var account = await session.LoadAsync<UserAccount>(userId);
            if (account is null || account.OrganizationId != organizationId || account.Status != AccountStatus.Active)
            {
                context.RejectPrincipal();
                return;
            }

            var securityStamp = principal.FindFirstValue(IdentityClaims.SecurityStampClaim);
            if (!string.Equals(securityStamp, account.SecurityStamp, StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                return;
            }

            var roles = await IdentityRoleLookup.LoadAsync(session, account.RoleIds);
            var permissions = roles.SelectMany(role => role.Permissions).Distinct(StringComparer.Ordinal).ToArray();
            var oldPermissions = principal.FindAll(AuthenticatedActor.PermissionClaim)
                .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
            if (!oldPermissions.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(permissions.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal) ||
                principal.FindFirstValue(ClaimTypes.Email) != account.Email ||
                principal.FindFirstValue(ClaimTypes.Name) != account.DisplayName)
            {
                context.ReplacePrincipal(IdentityClaims.Create(account, permissions));
                context.ShouldRenew = true;
            }
        }
        catch (Exception exception)
        {
            // A failed revocation check fails closed. The user can sign in again when storage recovers.
            logger.LogWarning(exception, "Unable to validate an authenticated identity cookie.");
            context.RejectPrincipal();
        }
    }
}
