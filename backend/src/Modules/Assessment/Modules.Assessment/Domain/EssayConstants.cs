using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Assessment.Domain;

public static class EssayConstants
{
    public static Guid TopicId(Guid assignmentId, Guid groupId) =>
        StableId($"essay-topic:{assignmentId:D}:{groupId:D}");

    public static Guid GradeItemId(Guid assignmentId) =>
        StableId($"essay-grade-item:{assignmentId:D}");

    public static Guid GradeEntryId(Guid assignmentId, Guid groupId, Guid studentId) =>
        StableId($"essay-grade-entry:{assignmentId:D}:{groupId:D}:{studentId:D}");

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
