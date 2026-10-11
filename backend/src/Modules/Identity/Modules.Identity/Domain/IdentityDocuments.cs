namespace LearningPlatform.Identity.Domain;

public enum OrganizationStatus
{
    Active,
    Suspended
}

public enum AccountStatus
{
    Active,
    Disabled
}

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string NormalizedCode { get; set; } = "";
    public string Name { get; set; } = "";
    public OrganizationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public AccountStatus Status { get; set; }
    public string SecurityStamp { get; set; } = "";
    public Guid[] RoleIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RoleDefinition
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string[] Permissions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class IdentityBootstrapState
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AdministratorId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}
