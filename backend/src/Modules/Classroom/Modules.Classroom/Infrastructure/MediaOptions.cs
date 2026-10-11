using System.ComponentModel.DataAnnotations;

namespace LearningPlatform.Classroom.Infrastructure.Media;

// Business APIs issue scoped room access; video/audio travel directly to the SFU.
public sealed class MediaOptions
{
    [Required] public string ServerUrl { get; set; } = "";
}
