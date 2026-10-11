using Microsoft.AspNetCore.Authorization;

namespace LearningPlatform.Identity.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
