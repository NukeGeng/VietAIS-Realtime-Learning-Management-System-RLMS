using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Learning.Domain;

public static class LearningConstants
{
    public static Guid ProgressId(Guid activityId, Guid studentId) =>
        StableId($"learning-progress:{activityId:D}:{studentId:D}");

    public static Guid HistoryId(Guid activityId, Guid studentId, int gradeVersion) =>
        StableId($"learning-history:{activityId:D}:{studentId:D}:{gradeVersion}");

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
