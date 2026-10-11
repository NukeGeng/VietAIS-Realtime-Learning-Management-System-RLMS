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

namespace LearningPlatform.Identity.Api;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        LearningPlatform.Identity.Features.Identity.Bootstrap.BootstrapEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.Login.LoginEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.GetMe.GetMeEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.GetOrganization.GetOrganizationEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.UpdateOrganization.UpdateOrganizationEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.GetRoles.GetRolesEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.CreateRole.CreateRoleEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.UpdateRole.UpdateRoleEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.DeleteRole.DeleteRoleEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.GetUsers.GetUsersEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.CreateUser.CreateUserEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.UpdateUser.UpdateUserEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.DisableUser.DisableUserEndpoint.Map(endpoints);
        LearningPlatform.Identity.Features.Identity.EnableUser.EnableUserEndpoint.Map(endpoints);
        endpoints.MapPost("/api/identity/logout", (Delegate)LogoutAsync);
    }

    internal static async Task<IResult> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync();
        return Results.NoContent();
    }


    internal static bool IsBuiltInRole(Guid organizationId, RoleDefinition role) =>
        PermissionCatalog.BuiltInRoles.Keys.Any(name =>
            role.Id == DomainIdentityConstants.RoleId(organizationId, name));


    internal static async Task<IResult> ChangeUserStatusAsync(
        Guid userId, AccountStatus target, HttpContext context, IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        if (actor!.UserId == userId)
        {
            return Results.Conflict(new { error = "An administrator cannot change their own account status." });
        }

        var account = await session.LoadAsync<UserAccount>(userId, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        if (account.Status == target)
        {
            return Results.Ok(AccountResponse.From(account, []));
        }

        account.Status = target;
        account.SecurityStamp = CreateSecurityStamp();
        account.UpdatedAt = DateTimeOffset.UtcNow;
        session.Update(account);
        await session.SaveChangesAsync(cancellationToken);
        return Results.Ok(AccountResponse.From(account, []));
    }


    internal static string RoleDisplayName(string normalizedName) => normalizedName switch
    {
        "ADMIN" => "Administrator",
        "TEACHER" => "Teacher",
        "STUDENT" => "Student",
        _ => normalizedName
    };


    internal static string CreateSecurityStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));


    internal static async Task<RoleDefinition?> FindRoleByNameAsync(IQuerySession session,
        Guid organizationId, string normalizedName, CancellationToken cancellationToken)
    {
        var deterministicId = DomainIdentityConstants.RoleId(organizationId, normalizedName);
        return await session.LoadAsync<RoleDefinition>(deterministicId, cancellationToken) ??
            await session.Query<RoleDefinition>().FirstOrDefaultAsync(role =>
                role.NormalizedName == normalizedName, cancellationToken);
    }


    internal static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

}
