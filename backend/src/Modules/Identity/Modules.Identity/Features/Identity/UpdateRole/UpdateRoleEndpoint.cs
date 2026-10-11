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

namespace LearningPlatform.Identity.Features.Identity.UpdateRole;

internal static class UpdateRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/identity/roles/{roleId:guid}", UpdateRoleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
    }


    private static async Task<IResult> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var role = await session.LoadAsync<RoleDefinition>(roleId, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        if (IsBuiltInRole(actor!.OrganizationId, role))
        {
            return Results.Conflict(new { error = "Built-in roles cannot be changed." });
        }

        try
        {
            var name = request.Name is null ? role.NormalizedName : IdentityRules.NormalizeRoleName(request.Name);
            if (PermissionCatalog.BuiltInRoles.ContainsKey(name))
            {
                return Results.Conflict(new { error = "A built-in role name is reserved." });
            }

            var permissions = request.Permissions is null
                ? role.Permissions
                : IdentityRules.RequirePermissions(request.Permissions, PermissionCatalog.All);
            if (permissions.Any(permission => !actor.Permissions.Contains(permission,
                    StringComparer.Ordinal)))
            {
                return Results.Forbid();
            }

            role.Name = name;
            role.NormalizedName = name;
            role.Permissions = permissions.Distinct(StringComparer.Ordinal).ToArray();
            session.Update(role);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(new RoleResponse(role.Id, role.Name, role.Permissions));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A role with that name already exists." });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "A role with that name already exists." });
        }
    }

}
