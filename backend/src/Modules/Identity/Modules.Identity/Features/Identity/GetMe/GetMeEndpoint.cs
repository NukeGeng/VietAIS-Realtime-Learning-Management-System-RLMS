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

namespace LearningPlatform.Identity.Features.Identity.GetMe;

internal static class GetMeEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/identity/me", GetMeAsync);
    }


    private static async Task<IResult> GetMeAsync(
        HttpContext context, IQuerySession session, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var account = await session.LoadAsync<UserAccount>(actor!.UserId, cancellationToken);
        return account is null || account.OrganizationId != actor.OrganizationId || account.Status != AccountStatus.Active
            ? Results.Forbid()
            : Results.Ok(AccountResponse.From(account, actor.Permissions));
    }

}
