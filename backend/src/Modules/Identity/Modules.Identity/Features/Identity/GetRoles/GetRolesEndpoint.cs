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

namespace LearningPlatform.Identity.Features.Identity.GetRoles;

internal static class GetRolesEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/identity/roles", GetRolesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
    }


    private static async Task<IResult> GetRolesAsync(IQuerySession session, CancellationToken cancellationToken)
    {
        var roles = await session.Query<RoleDefinition>().OrderBy(role => role.Name).ToListAsync(cancellationToken);
        return Results.Ok(roles.Select(role => new RoleResponse(role.Id, role.Name, role.Permissions)));
    }

}
