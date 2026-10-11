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

namespace LearningPlatform.Identity.Features.Identity.CreateRole;

internal static class CreateRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/identity/roles", CreateRoleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
    }


    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var name = IdentityRules.NormalizeRoleName(request.Name);
            if (PermissionCatalog.BuiltInRoles.ContainsKey(name))
            {
                return Results.Conflict(new { error = "Built-in roles cannot be recreated." });
            }

            var permissions = IdentityRules.RequirePermissions(request.Permissions, PermissionCatalog.All);
            if (permissions.Any(permission => !actor!.Permissions.Contains(permission,
                    StringComparer.Ordinal)))
            {
                return Results.Forbid();
            }

            var now = DateTimeOffset.UtcNow;
            var role = new RoleDefinition
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                Name = name,
                NormalizedName = name,
                Permissions = permissions,
                CreatedAt = now
            };
            session.Insert(role);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/identity/roles/{role.Id:D}",
                new RoleResponse(role.Id, role.Name, role.Permissions));
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
