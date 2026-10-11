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

namespace LearningPlatform.Identity.Features.Identity.DeleteRole;

internal static class DeleteRoleEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/api/identity/roles/{roleId:guid}", DeleteRoleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
    }


    private static async Task<IResult> DeleteRoleAsync(
        Guid roleId,
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
            return Results.Conflict(new { error = "Built-in roles cannot be deleted." });
        }

        if (await session.Query<UserAccount>().AnyAsync(user => user.RoleIds.Contains(roleId),
                cancellationToken))
        {
            return Results.Conflict(new { error = "A role assigned to a user cannot be deleted." });
        }

        session.Delete(role);
        await session.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

}
