using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Academics.Domain;

public static class AcademicConstants
{
    public static Guid EnrollmentId(Guid learningClassId, Guid studentId) =>
        StableId($"enrollment:{learningClassId:D}:{studentId:D}");

    public static Guid TeacherAssignmentId(Guid learningClassId, Guid teacherId) =>
        StableId($"teacher-assignment:{learningClassId:D}:{teacherId:D}");

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16]);
    }
}
