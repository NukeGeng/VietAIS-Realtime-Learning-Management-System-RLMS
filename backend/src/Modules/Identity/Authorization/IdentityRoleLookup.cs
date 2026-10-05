using LearningPlatform.Identity.Domain;
using Marten;

namespace LearningPlatform.Identity.Authorization;

public static class IdentityRoleLookup
{
    public static async Task<IReadOnlyList<RoleDefinition>> LoadAsync(
        IQuerySession session, IEnumerable<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var ids = roleIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await session.Query<RoleDefinition>()
            .Where(role => ids.Contains(role.Id))
            .ToListAsync(cancellationToken);
    }
}
