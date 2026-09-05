using System.Security.Cryptography;
using System.Text;

namespace LearningPlatform.Classroom.Domain;

public static class ClassroomConstants
{
    public static Guid AttendanceRecordId(Guid liveSessionId, Guid studentId) =>
        DeterministicId("attendance", liveSessionId, studentId);

    public static Guid RaiseHandId(Guid liveSessionId, Guid studentId) =>
        DeterministicId("raise-hand", liveSessionId, studentId);

    private static Guid DeterministicId(string kind, Guid liveSessionId, Guid userId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"rlms:{kind}:{liveSessionId:D}:{userId:D}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
