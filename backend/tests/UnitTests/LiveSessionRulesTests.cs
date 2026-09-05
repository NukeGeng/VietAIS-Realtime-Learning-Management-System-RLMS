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
}
