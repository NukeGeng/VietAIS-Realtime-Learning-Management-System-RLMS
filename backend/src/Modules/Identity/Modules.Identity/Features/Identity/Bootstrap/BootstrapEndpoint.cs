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

namespace LearningPlatform.Identity.Features.Identity.Bootstrap;

internal static class BootstrapEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/identity/bootstrap", BootstrapAsync).AllowAnonymous();
    }


    private static async Task<IResult> BootstrapAsync(
        BootstrapRequest request,
        HttpRequest httpRequest,
        IDocumentStore store,
        IPasswordHasher<UserAccount> passwordHasher,
        IOptions<IdentityDomainOptions> options,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(options.Value.BootstrapToken) ||
            !FixedTimeEquals(httpRequest.Headers["X-RLMS-Bootstrap-Token"].ToString(), options.Value.BootstrapToken))
        {
            return Results.NotFound();
        }

        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        try
        {
            var code = IdentityRules.NormalizeCode(request.OrganizationCode);
            var name = IdentityRules.RequireDisplayName(request.OrganizationName);
            var email = IdentityRules.NormalizeEmail(request.AdminEmail);
            var displayName = IdentityRules.RequireDisplayName(request.AdminDisplayName);
            var password = IdentityRules.RequirePassword(request.AdminPassword);
            var organizationId = DomainIdentityConstants.OrganizationId(code);
            var userId = DomainIdentityConstants.UserId(organizationId, email);

            await using var control = store.LightweightSession(DomainIdentityConstants.ControlPlaneTenantId);
            var bootstrapCompleted = await control.LoadAsync<IdentityBootstrapState>(
                DomainIdentityConstants.BootstrapStateId, cancellationToken) is not null;
            var organizationExists = await control.LoadAsync<Organization>(organizationId,
                cancellationToken) is not null;
            // Local demo setup may need a second isolated tenant while the
            // one-time platform bootstrap has already created another tenant.
            // Keep this path explicitly development-only and token-protected;
            // production retains the original one-organization bootstrap rule.
            var allowAdditionalDevelopmentOrganization = environment.IsDevelopment() &&
                httpRequest.Headers["X-RLMS-Allow-Additional-Development-Organization"]
                    .ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            if ((bootstrapCompleted && !allowAdditionalDevelopmentOrganization) ||
                organizationExists)
            {
                return Results.Conflict(new { error = "RLMS has already been bootstrapped or the organization exists." });
            }

            var now = DateTimeOffset.UtcNow;
            var organization = new Organization
            {
                Id = organizationId,
                Code = code,
                NormalizedCode = code,
                Name = name,
                Status = OrganizationStatus.Active,
                CreatedAt = now
            };
            control.Insert(organization);
            await control.SaveChangesAsync(cancellationToken);

            try
            {
                await using var tenant = store.LightweightSession(organizationId.ToString("D"));
                foreach (var role in PermissionCatalog.BuiltInRoles)
                {
                    tenant.Insert(new RoleDefinition
                    {
                        Id = DomainIdentityConstants.RoleId(organizationId, role.Key),
                        OrganizationId = organizationId,
                        Name = RoleDisplayName(role.Key),
                        NormalizedName = role.Key,
                        Permissions = role.Value,
                        CreatedAt = now
                    });
                }

                var admin = new UserAccount
                {
                    Id = userId,
                    OrganizationId = organizationId,
                    Email = email,
                    NormalizedEmail = email,
                    DisplayName = displayName,
                    PasswordHash = passwordHasher.HashPassword(new UserAccount(), password),
                    Status = AccountStatus.Active,
                    SecurityStamp = CreateSecurityStamp(),
                    RoleIds = [DomainIdentityConstants.RoleId(organizationId, "ADMIN")],
                    CreatedAt = now,
                    UpdatedAt = now
                };
                tenant.Insert(admin);
                await tenant.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await using var cleanup = store.LightweightSession(DomainIdentityConstants.ControlPlaneTenantId);
                cleanup.Delete<Organization>(organizationId);
                await cleanup.SaveChangesAsync(cancellationToken);
                throw;
            }

            if (!bootstrapCompleted)
            {
                await using var completed = store.LightweightSession(
                    DomainIdentityConstants.ControlPlaneTenantId);
                completed.Insert(new IdentityBootstrapState
                {
                    Id = DomainIdentityConstants.BootstrapStateId,
                    OrganizationId = organizationId,
                    AdministratorId = userId,
                    CompletedAt = now
                });
                await completed.SaveChangesAsync(cancellationToken);
            }
            return Results.Created("/api/identity/me", new BootstrapResponse(organizationId, userId));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "RLMS has already been bootstrapped or the organization exists." });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "RLMS has already been bootstrapped or the organization exists." });
        }
    }

}
