using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Reporting.Domain;

public static class ReportingConstants
{
    public static Guid ClassSnapshotId(Guid classId) => StableId($"reporting-class:{classId:D}");

    public static Guid StudentSnapshotId(Guid classId, Guid studentId) =>
        StableId($"reporting-student:{classId:D}:{studentId:D}");

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
