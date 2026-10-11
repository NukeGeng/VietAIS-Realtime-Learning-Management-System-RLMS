using System.ComponentModel.DataAnnotations;

namespace LearningPlatform.Identity.Domain;

public sealed class IdentityOptions
{
    public const string Section = "Identity";

    // Empty disables the one-time bootstrap route. It must never be a default credential.
    public string BootstrapToken { get; set; } = "";

    [Range(8, 120)]
    public int SessionHours { get; set; } = 8;
}
