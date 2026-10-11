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

namespace LearningPlatform.Identity.Features.Identity.Login;

internal static class LoginEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/identity/login", LoginAsync).AllowAnonymous();
    }


    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IDocumentStore store,
        IPasswordHasher<UserAccount> passwordHasher,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var code = IdentityRules.NormalizeCode(request.OrganizationCode);
            var email = IdentityRules.NormalizeEmail(request.Email);
            var password = IdentityRules.RequirePassword(request.Password);
            await using var control = store.QuerySession(DomainIdentityConstants.ControlPlaneTenantId);
            var organization = await control.LoadAsync<Organization>(DomainIdentityConstants.OrganizationId(code), cancellationToken);
            if (organization is null || organization.Status != OrganizationStatus.Active)
            {
                return Results.Unauthorized();
            }

            await using var tenant = store.QuerySession(organization.Id.ToString("D"));
            var account = await tenant.LoadAsync<UserAccount>(DomainIdentityConstants.UserId(organization.Id, email), cancellationToken);
            var passwordResult = account is null
                ? PasswordVerificationResult.Failed
                : passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
            if (account is null || account.Status != AccountStatus.Active ||
                account.OrganizationId != organization.Id || passwordResult == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var roles = await IdentityRoleLookup.LoadAsync(tenant, account.RoleIds, cancellationToken);
            var permissions = roles.SelectMany(role => role.Permissions)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                await using var update = store.LightweightSession(organization.Id.ToString("D"));
                account.PasswordHash = passwordHasher.HashPassword(account, password);
                account.UpdatedAt = DateTimeOffset.UtcNow;
                update.Update(account);
                await update.SaveChangesAsync(cancellationToken);
            }

            await context.SignInAsync(IdentityClaims.Create(account, permissions), new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });
            return Results.Ok(AccountResponse.From(account, permissions));
        }
        catch (IdentityValidationException)
        {
            return Results.Unauthorized();
        }
    }

}
