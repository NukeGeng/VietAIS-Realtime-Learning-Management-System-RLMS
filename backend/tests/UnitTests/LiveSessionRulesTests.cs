using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;

namespace LearningPlatform.UnitTests;

public sealed class LiveSessionRulesTests
{
    [Fact]
    public void Media_room_id_is_preserved_when_valid()
    {
        Assert.Equal("room-123", LiveSessionRules.RequireMediaRoomId("room-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_media_room_id_is_rejected(string value)
    {
        Assert.Throws<LiveSessionValidationException>(() => LiveSessionRules.RequireMediaRoomId(value));
    }

    [Fact]
    public void Oversized_media_room_id_is_rejected()
    {
        Assert.Throws<LiveSessionValidationException>(() =>
            LiveSessionRules.RequireMediaRoomId(new string('x', 129)));
    }

    [Fact]
    public void Presence_keys_are_scoped_to_the_tenant()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantA = LivePresenceStore.Key(Guid.NewGuid(), sessionId, userId);
        var tenantB = LivePresenceStore.Key(Guid.NewGuid(), sessionId, userId);

        Assert.NotEqual(tenantA, tenantB);
        Assert.StartsWith("rlms:live-presence:", tenantA, StringComparison.Ordinal);
    }

    [Fact]
    public void Attendance_and_raise_hand_ids_are_stable_per_session_and_student()
    {
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        Assert.Equal(ClassroomConstants.AttendanceRecordId(sessionId, studentId),
            ClassroomConstants.AttendanceRecordId(sessionId, studentId));
        Assert.Equal(ClassroomConstants.RaiseHandId(sessionId, studentId),
            ClassroomConstants.RaiseHandId(sessionId, studentId));
        Assert.NotEqual(ClassroomConstants.AttendanceRecordId(sessionId, studentId),
            ClassroomConstants.AttendanceRecordId(sessionId, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Attendance_adjustment_requires_a_reason(string? reason)
    {
        Assert.Throws<ClassroomInteractionValidationException>(() =>
            ClassroomInteractionRules.RequireAdjustmentReason(reason));
    }

    [Fact]
    public void Attendance_adjustment_reason_is_bounded()
    {
        Assert.Throws<ClassroomInteractionValidationException>(() =>
            ClassroomInteractionRules.RequireAdjustmentReason(new string('x',
                ClassroomInteractionRules.MaxAttendanceAdjustmentReasonLength + 1)));
    }
}
