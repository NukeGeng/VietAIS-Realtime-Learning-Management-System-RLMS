using System.ComponentModel.DataAnnotations;

namespace LearningPlatform.BuildingBlocks.Storage;

public sealed class ObjectStorageOptions
{
    public const string Section = "ObjectStorage";
    [Required, Url] public string Endpoint { get; set; } = "";
    [Required] public string Region { get; set; } = "us-east-1";
    [Required] public string Bucket { get; set; } = "";
    [Required] public string AccessKey { get; set; } = "";
    [Required] public string SecretKey { get; set; } = "";
}
