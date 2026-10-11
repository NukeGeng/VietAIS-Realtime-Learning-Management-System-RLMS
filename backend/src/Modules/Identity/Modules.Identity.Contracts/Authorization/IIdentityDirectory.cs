using LearningPlatform.Identity.Domain;

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
