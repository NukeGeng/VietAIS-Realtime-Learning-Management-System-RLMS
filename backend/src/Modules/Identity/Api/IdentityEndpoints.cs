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
using Microsoft.Extensions.Options;
using Npgsql;
using IdentityDomainOptions = LearningPlatform.Identity.Domain.IdentityOptions;
using DomainIdentityConstants = LearningPlatform.Identity.Domain.IdentityConstants;

namespace LearningPlatform.Identity.Api;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/identity/bootstrap", BootstrapAsync).AllowAnonymous();
        endpoints.MapPost("/api/identity/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/api/identity/logout", (Delegate)LogoutAsync);
        endpoints.MapGet("/api/identity/me", GetMeAsync);
        endpoints.MapGet("/api/identity/roles", GetRolesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
        endpoints.MapGet("/api/identity/users", GetUsersAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
        endpoints.MapPost("/api/identity/users", CreateUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
    }

    private static async Task<IResult> BootstrapAsync(
        BootstrapRequest request,
        HttpRequest httpRequest,
        IDocumentStore store,
        IPasswordHasher<UserAccount> passwordHasher,
        IOptions<IdentityDomainOptions> options,
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
            if (await control.LoadAsync<IdentityBootstrapState>(DomainIdentityConstants.BootstrapStateId, cancellationToken) is not null ||
                await control.LoadAsync<Organization>(organizationId, cancellationToken) is not null)
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

            await using var completed = store.LightweightSession(DomainIdentityConstants.ControlPlaneTenantId);
            completed.Insert(new IdentityBootstrapState
            {
                Id = DomainIdentityConstants.BootstrapStateId,
                OrganizationId = organizationId,
                AdministratorId = userId,
                CompletedAt = now
            });
            await completed.SaveChangesAsync(cancellationToken);
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

    private static async Task<IResult> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync();
        return Results.NoContent();
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

    private static async Task<IResult> GetRolesAsync(IQuerySession session, CancellationToken cancellationToken)
    {
        var roles = await session.Query<RoleDefinition>().OrderBy(role => role.Name).ToListAsync(cancellationToken);
        return Results.Ok(roles.Select(role => new RoleResponse(role.Id, role.Name, role.Permissions)));
    }

    private static async Task<IResult> GetUsersAsync(
        int? page, int? pageSize, IQuerySession session, CancellationToken cancellationToken)
    {
        var safePage = Math.Max(page ?? 1, 1);
        var safePageSize = Math.Clamp(pageSize ?? 50, 1, 100);
        var users = await session.Query<UserAccount>().OrderBy(user => user.DisplayName)
            .Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync(cancellationToken);
        return Results.Ok(users.Select(user => AccountResponse.From(user, [])));
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
                var role = await session.LoadAsync<RoleDefinition>(
                    DomainIdentityConstants.RoleId(actor.OrganizationId, roleName), cancellationToken);
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

    private static string RoleDisplayName(string normalizedName) => normalizedName switch
    {
        "ADMIN" => "Administrator",
        "TEACHER" => "Teacher",
        "STUDENT" => "Student",
        _ => normalizedName
    };

    private static string CreateSecurityStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

}

public sealed record BootstrapRequest(
    string OrganizationCode,
    string OrganizationName,
    string AdminEmail,
    string AdminDisplayName,
    string AdminPassword);

public sealed record LoginRequest(string OrganizationCode, string Email, string Password);

public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string[]? Roles);

public sealed record BootstrapResponse(Guid OrganizationId, Guid AdministratorId);

public sealed record RoleResponse(Guid Id, string Name, string[] Permissions);

public sealed record AccountResponse(
    Guid Id,
    Guid OrganizationId,
    string Email,
    string DisplayName,
    AccountStatus Status,
    IReadOnlyCollection<string> Permissions)
{
    public static AccountResponse From(UserAccount account, IEnumerable<string> permissions) =>
        new(account.Id, account.OrganizationId, account.Email, account.DisplayName,
            account.Status, permissions.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
}
