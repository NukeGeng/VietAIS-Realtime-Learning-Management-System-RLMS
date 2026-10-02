using LearningPlatform.Identity.Domain;
using Marten;

namespace LearningPlatform.Identity.Authorization;

public interface IIdentityDirectory
{
    // Callers authorize the class/attempt first and pass only its member IDs.
    // Historical rows may refer to disabled accounts, so name lookup retains them.
    Task<IReadOnlyDictionary<Guid, string>> FindMemberNamesAsync(Guid organizationId,
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    Task<IdentityMember?> FindActiveMemberAsync(Guid organizationId, Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid organizationId, Guid userId, string permission,
        CancellationToken cancellationToken = default);
}

public sealed record IdentityMember(Guid Id, Guid OrganizationId, string DisplayName);

public sealed class IdentityDirectory(IDocumentStore store) : IIdentityDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> FindMemberNamesAsync(Guid organizationId,
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (organizationId == Guid.Empty || ids.Length == 0)
            return new Dictionary<Guid, string>();

        await using var tenant = store.QuerySession(organizationId.ToString("D"));
        var accounts = await tenant.Query<UserAccount>()
            .Where(account => account.OrganizationId == organizationId && ids.Contains(account.Id))
            .Select(account => new { account.Id, account.DisplayName })
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(account => account.Id, account => account.DisplayName);
    }

    public async Task<IdentityMember?> FindActiveMemberAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        await using var control = store.QuerySession(IdentityConstants.ControlPlaneTenantId);
        var organization = await control.LoadAsync<Organization>(organizationId, cancellationToken);
        if (organization is null || organization.Status != OrganizationStatus.Active)
        {
            return null;
        }

        await using var tenant = store.QuerySession(organizationId.ToString("D"));
        var account = await tenant.LoadAsync<UserAccount>(userId, cancellationToken);
        return account is not null && account.Status == AccountStatus.Active &&
               account.OrganizationId == organizationId
            ? new IdentityMember(account.Id, account.OrganizationId, account.DisplayName)
            : null;
    }

    public async Task<bool> HasPermissionAsync(
        Guid organizationId, Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        var member = await FindActiveMemberAsync(organizationId, userId, cancellationToken);
        if (member is null)
        {
            return false;
        }

        await using var tenant = store.QuerySession(member.OrganizationId.ToString("D"));
        var account = await tenant.LoadAsync<UserAccount>(userId, cancellationToken);
        if (account is null)
        {
            return false;
        }

        var roles = await IdentityRoleLookup.LoadAsync(tenant, account.RoleIds, cancellationToken);
        return roles.SelectMany(role => role.Permissions)
            .Contains(permission, StringComparer.Ordinal);
    }
}
