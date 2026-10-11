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

namespace LearningPlatform.Identity.Features.Identity.CreateUser;

internal static class CreateUserEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/identity/users", CreateUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
    }


    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        HttpContext context,
        IDocumentSession session,
        IPasswordHasher<UserAccount> passwordHasher,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var email = IdentityRules.NormalizeEmail(request.Email);
            var displayName = IdentityRules.RequireDisplayName(request.DisplayName);
            var password = IdentityRules.RequirePassword(request.Password);
            var roleNames = request.Roles?.Select(IdentityRules.NormalizeRoleName).Distinct(StringComparer.Ordinal).ToArray() ?? [];
            if (roleNames.Length == 0 || roleNames.Length > 10)
            {
                return Results.BadRequest(new { error = "One to ten valid roles are required." });
            }

            var roleIds = new List<Guid>(roleNames.Length);
            var callerPermissions = actor!.Permissions.ToHashSet(StringComparer.Ordinal);
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

            var now = DateTimeOffset.UtcNow;
            var account = new UserAccount
            {
                Id = DomainIdentityConstants.UserId(actor.OrganizationId, email),
                OrganizationId = actor.OrganizationId,
                Email = email,
                NormalizedEmail = email,
                DisplayName = displayName,
                PasswordHash = passwordHasher.HashPassword(new UserAccount(), password),
                Status = AccountStatus.Active,
                SecurityStamp = CreateSecurityStamp(),
                RoleIds = roleIds.ToArray(),
                CreatedAt = now,
                UpdatedAt = now
            };
            session.Insert(account);
            await session.SaveChangesAsync(cancellationToken);
            var permissions = (await IdentityRoleLookup.LoadAsync(session, roleIds, cancellationToken))
                .SelectMany(role => role.Permissions).Distinct(StringComparer.Ordinal).ToArray();
            return Results.Created($"/api/identity/users/{account.Id:D}", AccountResponse.From(account, permissions));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "An account with that email already exists in this organization." });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "An account with that email already exists in this organization." });
        }
    }

}
