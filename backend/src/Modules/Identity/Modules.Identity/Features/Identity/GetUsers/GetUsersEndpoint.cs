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

namespace LearningPlatform.Identity.Features.Identity.GetUsers;

internal static class GetUsersEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/identity/users", GetUsersAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
    }


    private static async Task<IResult> GetUsersAsync(
        int? page, int? pageSize, IQuerySession session, CancellationToken cancellationToken)
    {
        var safePageSize = Math.Clamp(pageSize ?? 50, 1, 100);
        // Keep the computed Skip value inside Int32 for the LINQ provider even
        // when a client sends the largest possible page number.
        var maxPage = (int)Math.Min(int.MaxValue,
            (long)int.MaxValue / safePageSize + 1);
        var safePage = Math.Clamp(page ?? 1, 1, maxPage);
        var users = await session.Query<UserAccount>().OrderBy(user => user.DisplayName)
            .Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
        return Results.Ok(users.Select(user => AccountResponse.From(user, [])));
    }

}
