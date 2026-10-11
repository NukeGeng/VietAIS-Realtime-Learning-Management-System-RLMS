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

namespace LearningPlatform.Identity.Features.Identity.UpdateOrganization;

internal static class UpdateOrganizationEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/identity/organization", UpdateOrganizationAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageOrganizations);
    }


    private static async Task<IResult> UpdateOrganizationAsync(
        UpdateOrganizationRequest request, HttpContext context, IDocumentStore store,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        await using var control = store.LightweightSession(DomainIdentityConstants.ControlPlaneTenantId);
        var organization = await control.LoadAsync<Organization>(actor!.OrganizationId,
            cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        try
        {
            organization.Name = IdentityRules.RequireDisplayName(request.Name);
            control.Update(organization);
            await control.SaveChangesAsync(cancellationToken);
            return Results.Ok(OrganizationResponse.From(organization));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

}
