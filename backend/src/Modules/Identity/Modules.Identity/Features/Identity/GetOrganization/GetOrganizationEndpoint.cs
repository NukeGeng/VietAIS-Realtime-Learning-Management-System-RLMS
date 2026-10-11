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

namespace LearningPlatform.Identity.Features.Identity.GetOrganization;

internal static class GetOrganizationEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/identity/organization", GetOrganizationAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
    }


    private static async Task<IResult> GetOrganizationAsync(
        HttpContext context, IDocumentStore store, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        await using var control = store.QuerySession(DomainIdentityConstants.ControlPlaneTenantId);
        var organization = await control.LoadAsync<Organization>(actor!.OrganizationId,
            cancellationToken);
        return organization is null
            ? Results.NotFound()
            : Results.Ok(OrganizationResponse.From(organization));
    }

}
