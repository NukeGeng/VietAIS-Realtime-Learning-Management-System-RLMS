using LearningPlatform.Identity.Api;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JasperFx;
using LearningPlatform.Identity.Authorization;
using LearningPlatform.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using IdentityDomainOptions = LearningPlatform.Identity.Domain.IdentityOptions;
using DomainIdentityConstants = LearningPlatform.Identity.Domain.IdentityConstants;
using static LearningPlatform.Identity.Api.IdentityEndpoints;

namespace LearningPlatform.Identity.Features.Identity.UpdateUser;

internal static class UpdateUserEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/identity/users/{userId:guid}", UpdateUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
    }


    private static async Task<IResult> UpdateUserAsync(
        Guid userId, UpdateUserRequest request, HttpContext context, IDocumentSession session,
        IPasswordHasher<UserAccount> passwordHasher, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var account = await session.LoadAsync<UserAccount>(userId, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        var callerPermissions = actor!.Permissions.ToHashSet(StringComparer.Ordinal);
        var changed = false;
        try
        {
            if (request.DisplayName is not null)
            {
                var displayName = IdentityRules.RequireDisplayName(request.DisplayName);
                changed |= !string.Equals(account.DisplayName, displayName, StringComparison.Ordinal);
                account.DisplayName = displayName;
            }

            if (request.Password is not null)
            {
                var password = IdentityRules.RequirePassword(request.Password);
                account.PasswordHash = passwordHasher.HashPassword(account, password);
                account.SecurityStamp = CreateSecurityStamp();
                changed = true;
            }

            if (request.Roles is not null)
            {
                var roleNames = request.Roles.Select(IdentityRules.NormalizeRoleName)
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (roleNames.Length == 0 || roleNames.Length > 10)
                {
                    return Results.BadRequest(new { error = "One to ten valid roles are required." });
                }

                var roleIds = new List<Guid>(roleNames.Length);
                foreach (var roleName in roleNames)
                {
                    var role = await FindRoleByNameAsync(session, actor.OrganizationId, roleName,
                        cancellationToken);
                    if (role is null)
                    {
                        return Results.BadRequest(new { error = $"Role '{roleName}' does not exist." });
                    }

                    if (role.Permissions.Any(permission => !callerPermissions.Contains(permission)))
                    {
                        return Results.Forbid();
                    }

                    roleIds.Add(role.Id);
                }

                if (!account.RoleIds.OrderBy(value => value)
                        .SequenceEqual(roleIds.OrderBy(value => value)))
                {
                    account.RoleIds = roleIds.ToArray();
                    account.SecurityStamp = CreateSecurityStamp();
                    changed = true;
                }
            }

            if (!changed)
            {
                var permissions = (await IdentityRoleLookup.LoadAsync(session, account.RoleIds,
                        cancellationToken)).SelectMany(role => role.Permissions)
                    .Distinct(StringComparer.Ordinal).ToArray();
                return Results.Ok(AccountResponse.From(account, permissions));
            }

            account.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(account);
            await session.SaveChangesAsync(cancellationToken);
            var updatedPermissions = (await IdentityRoleLookup.LoadAsync(session, account.RoleIds,
                    cancellationToken)).SelectMany(role => role.Permissions)
                .Distinct(StringComparer.Ordinal).ToArray();
            return Results.Ok(AccountResponse.From(account, updatedPermissions));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
