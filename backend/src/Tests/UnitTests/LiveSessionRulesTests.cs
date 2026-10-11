using LearningPlatform.Classroom.Domain;
using LearningPlatform.Classroom.Presence;
using LearningPlatform.Classroom.Attendance;
using LearningPlatform.Academics.Domain;

namespace LearningPlatform.UnitTests;

public sealed class LiveSessionRulesTests
{
    [Fact]
    public void Attendance_cutoff_uses_schedule_start_during_its_occurrence()
    {
        var scheduledStart = new DateTimeOffset(2026, 9, 9, 7, 0, 0, TimeSpan.FromHours(7));
        var schedule = new ClassSchedule { StartsAt = scheduledStart, EndsAt = scheduledStart.AddHours(4) };
        var liveSession = new LiveSession { StartedAt = scheduledStart.AddMinutes(10), LateAfterMinutes = 15 };

        Assert.Equal(scheduledStart.AddMinutes(15), AttendanceHandlerRules.LateCutoff(liveSession, schedule));
    }

    [Fact]
    public void Attendance_cutoff_uses_runtime_start_when_the_planned_occurrence_has_ended()
    {
        var scheduledStart = new DateTimeOffset(2026, 9, 9, 7, 0, 0, TimeSpan.FromHours(7));
        var runtimeStart = scheduledStart.AddDays(1);
        var schedule = new ClassSchedule { StartsAt = scheduledStart, EndsAt = scheduledStart.AddHours(4) };
        var liveSession = new LiveSession { StartedAt = runtimeStart, LateAfterMinutes = 15 };

        Assert.Equal(runtimeStart.AddMinutes(15), AttendanceHandlerRules.LateCutoff(liveSession, schedule));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public void Unscheduled_attendance_cutoff_uses_the_session_threshold(int lateAfterMinutes)
    {
        var startedAt = new DateTimeOffset(2026, 9, 9, 7, 0, 0, TimeSpan.FromHours(7));
        var liveSession = new LiveSession { StartedAt = startedAt, LateAfterMinutes = lateAfterMinutes };

        Assert.Equal(startedAt.AddMinutes(lateAfterMinutes), AttendanceHandlerRules.LateCutoff(liveSession, null));
        Assert.Null(AttendanceHandlerRules.LateCutoff(new LiveSession(), null));
    }

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
