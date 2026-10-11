using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Identity.Domain;

public static class IdentityConstants
{
    public const string ControlPlaneTenantId = "control";
    public static readonly Guid BootstrapStateId =
        Guid.Parse("a2d9551f-bcf7-4d8b-9f02-9e2a0d6df3b1");

    public static Guid OrganizationId(string normalizedCode) =>
        StableId($"organization:{normalizedCode}");

    public static Guid UserId(Guid organizationId, string normalizedEmail) =>
        StableId($"user:{organizationId:D}:{normalizedEmail}");

    public static Guid RoleId(Guid organizationId, string normalizedName) =>
        StableId($"role:{organizationId:D}:{normalizedName}");

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
