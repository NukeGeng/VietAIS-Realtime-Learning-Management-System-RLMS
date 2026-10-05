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
        endpoints.MapPost("/api/identity/bootstrap", BootstrapAsync).AllowAnonymous();
        endpoints.MapPost("/api/identity/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/api/identity/logout", (Delegate)LogoutAsync);
        endpoints.MapGet("/api/identity/me", GetMeAsync);
        endpoints.MapGet("/api/identity/organization", GetOrganizationAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ReadClasses);
        endpoints.MapPut("/api/identity/organization", UpdateOrganizationAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageOrganizations);
        endpoints.MapGet("/api/identity/roles", GetRolesAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
        endpoints.MapPost("/api/identity/roles", CreateRoleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
        endpoints.MapPut("/api/identity/roles/{roleId:guid}", UpdateRoleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
        endpoints.MapDelete("/api/identity/roles/{roleId:guid}", DeleteRoleAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageRoles);
        endpoints.MapGet("/api/identity/users", GetUsersAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
        endpoints.MapPost("/api/identity/users", CreateUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
        endpoints.MapPut("/api/identity/users/{userId:guid}", UpdateUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
        endpoints.MapPost("/api/identity/users/{userId:guid}/disable",
                DisableUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
        endpoints.MapPost("/api/identity/users/{userId:guid}/enable",
                EnableUserAsync)
            .RequireAuthorization(IdentityAuthorizationPolicies.ManageUsers);
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

    private static async Task<IResult> GetRolesAsync(IQuerySession session, CancellationToken cancellationToken)
    {
        var roles = await session.Query<RoleDefinition>().OrderBy(role => role.Name).ToListAsync(cancellationToken);
        return Results.Ok(roles.Select(role => new RoleResponse(role.Id, role.Name, role.Permissions)));
    }

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        try
        {
            var name = IdentityRules.NormalizeRoleName(request.Name);
            if (PermissionCatalog.BuiltInRoles.ContainsKey(name))
            {
                return Results.Conflict(new { error = "Built-in roles cannot be recreated." });
            }

            var permissions = IdentityRules.RequirePermissions(request.Permissions, PermissionCatalog.All);
            if (permissions.Any(permission => !actor!.Permissions.Contains(permission,
                    StringComparer.Ordinal)))
            {
                return Results.Forbid();
            }

            var now = DateTimeOffset.UtcNow;
            var role = new RoleDefinition
            {
                Id = Guid.NewGuid(),
                OrganizationId = actor!.OrganizationId,
                Name = name,
                NormalizedName = name,
                Permissions = permissions,
                CreatedAt = now
            };
            session.Insert(role);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/identity/roles/{role.Id:D}",
                new RoleResponse(role.Id, role.Name, role.Permissions));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A role with that name already exists." });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "A role with that name already exists." });
        }
    }

    private static async Task<IResult> UpdateRoleAsync(
        Guid roleId,
        UpdateRoleRequest request,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var role = await session.LoadAsync<RoleDefinition>(roleId, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        if (IsBuiltInRole(actor!.OrganizationId, role))
        {
            return Results.Conflict(new { error = "Built-in roles cannot be changed." });
        }

        try
        {
            var name = request.Name is null ? role.NormalizedName : IdentityRules.NormalizeRoleName(request.Name);
            if (PermissionCatalog.BuiltInRoles.ContainsKey(name))
            {
                return Results.Conflict(new { error = "A built-in role name is reserved." });
            }

            var permissions = request.Permissions is null
                ? role.Permissions
                : IdentityRules.RequirePermissions(request.Permissions, PermissionCatalog.All);
            if (permissions.Any(permission => !actor.Permissions.Contains(permission,
                    StringComparer.Ordinal)))
            {
                return Results.Forbid();
            }

            role.Name = name;
            role.NormalizedName = name;
            role.Permissions = permissions.Distinct(StringComparer.Ordinal).ToArray();
            session.Update(role);
            await session.SaveChangesAsync(cancellationToken);
            return Results.Ok(new RoleResponse(role.Id, role.Name, role.Permissions));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DocumentAlreadyExistsException)
        {
            return Results.Conflict(new { error = "A role with that name already exists." });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Results.Conflict(new { error = "A role with that name already exists." });
        }
    }

    private static async Task<IResult> DeleteRoleAsync(
        Guid roleId,
        HttpContext context,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var role = await session.LoadAsync<RoleDefinition>(roleId, cancellationToken);
        if (role is null)
        {
            return Results.NotFound();
        }

        if (IsBuiltInRole(actor!.OrganizationId, role))
        {
            return Results.Conflict(new { error = "Built-in roles cannot be deleted." });
        }

        if (await session.Query<UserAccount>().AnyAsync(user => user.RoleIds.Contains(roleId),
                cancellationToken))
        {
            return Results.Conflict(new { error = "A role assigned to a user cannot be deleted." });
        }

        session.Delete(role);
        await session.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static bool IsBuiltInRole(Guid organizationId, RoleDefinition role) =>
        PermissionCatalog.BuiltInRoles.Keys.Any(name =>
            role.Id == DomainIdentityConstants.RoleId(organizationId, name));

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

    private static async Task<IResult> UpdateUserAsync(
        Guid userId, UpdateUserRequest request, HttpContext context, IDocumentSession session,
        IPasswordHasher<UserAccount> passwordHasher, CancellationToken cancellationToken)
    {
        if (!AuthenticatedActor.TryCreate(context.User, out var actor))
        {
            return Results.Forbid();
        }

        var account = await session.LoadAsync<UserAccount>(userId, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        var callerPermissions = actor!.Permissions.ToHashSet(StringComparer.Ordinal);
        var changed = false;
        try
        {
            if (request.DisplayName is not null)
            {
                var displayName = IdentityRules.RequireDisplayName(request.DisplayName);
                changed |= !string.Equals(account.DisplayName, displayName, StringComparison.Ordinal);
                account.DisplayName = displayName;
            }

            if (request.Password is not null)
            {
                var password = IdentityRules.RequirePassword(request.Password);
                account.PasswordHash = passwordHasher.HashPassword(account, password);
                account.SecurityStamp = CreateSecurityStamp();
                changed = true;
            }

            if (request.Roles is not null)
            {
                var roleNames = request.Roles.Select(IdentityRules.NormalizeRoleName)
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (roleNames.Length == 0 || roleNames.Length > 10)
                {
                    return Results.BadRequest(new { error = "One to ten valid roles are required." });
                }

                var roleIds = new List<Guid>(roleNames.Length);
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

                if (!account.RoleIds.OrderBy(value => value)
                        .SequenceEqual(roleIds.OrderBy(value => value)))
                {
                    account.RoleIds = roleIds.ToArray();
                    account.SecurityStamp = CreateSecurityStamp();
                    changed = true;
                }
            }

            if (!changed)
            {
                var permissions = (await IdentityRoleLookup.LoadAsync(session, account.RoleIds,
                        cancellationToken)).SelectMany(role => role.Permissions)
                    .Distinct(StringComparer.Ordinal).ToArray();
                return Results.Ok(AccountResponse.From(account, permissions));
            }

            account.UpdatedAt = DateTimeOffset.UtcNow;
            session.Update(account);
            await session.SaveChangesAsync(cancellationToken);
            var updatedPermissions = (await IdentityRoleLookup.LoadAsync(session, account.RoleIds,
                    cancellationToken)).SelectMany(role => role.Permissions)
                .Distinct(StringComparer.Ordinal).ToArray();
            return Results.Ok(AccountResponse.From(account, updatedPermissions));
        }
        catch (IdentityValidationException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static Task<IResult> DisableUserAsync(Guid userId, HttpContext context,
        IDocumentSession session, CancellationToken cancellationToken) =>
        ChangeUserStatusAsync(userId, AccountStatus.Disabled, context, session, cancellationToken);

    private static Task<IResult> EnableUserAsync(Guid userId, HttpContext context,
        IDocumentSession session, CancellationToken cancellationToken) =>
        ChangeUserStatusAsync(userId, AccountStatus.Active, context, session, cancellationToken);

    private static async Task<IResult> ChangeUserStatusAsync(
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

    private static string RoleDisplayName(string normalizedName) => normalizedName switch
    {
        "ADMIN" => "Administrator",
        "TEACHER" => "Teacher",
        "STUDENT" => "Student",
        _ => normalizedName
    };

    private static string CreateSecurityStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static async Task<RoleDefinition?> FindRoleByNameAsync(IQuerySession session,
        Guid organizationId, string normalizedName, CancellationToken cancellationToken)
    {
        var deterministicId = DomainIdentityConstants.RoleId(organizationId, normalizedName);
        return await session.LoadAsync<RoleDefinition>(deterministicId, cancellationToken) ??
            await session.Query<RoleDefinition>().FirstOrDefaultAsync(role =>
                role.NormalizedName == normalizedName, cancellationToken);
    }

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

public sealed record UpdateUserRequest(string? DisplayName, string? Password, string[]? Roles);

public sealed record UpdateOrganizationRequest(string Name);

public sealed record CreateRoleRequest(string Name, string[]? Permissions);

public sealed record UpdateRoleRequest(string? Name, string[]? Permissions);

public sealed record BootstrapResponse(Guid OrganizationId, Guid AdministratorId);

public sealed record RoleResponse(Guid Id, string Name, string[] Permissions);

public sealed record OrganizationResponse(Guid Id, string Code, string Name,
    OrganizationStatus Status, DateTimeOffset CreatedAt)
{
    public static OrganizationResponse From(Organization organization) => new(organization.Id,
        organization.Code, organization.Name, organization.Status, organization.CreatedAt);
}

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
