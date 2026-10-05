using System.Net.Mail;
using System.Text;

namespace LearningPlatform.Identity.Domain;

public static class IdentityRules
{
    public static string NormalizeEmail(string? value)
    {
        var email = (value ?? "").Trim().Normalize(NormalizationForm.FormKC);
        if (email.Length is < 3 or > 320 || email.Contains('\r') || email.Contains('\n'))
        {
            throw new IdentityValidationException("Email must be between 3 and 320 characters.");
        }

        if (!MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new IdentityValidationException("Email format is invalid.");
        }

        return email.ToUpperInvariant();
    }

    public static string NormalizeCode(string? value)
    {
        var code = (value ?? "").Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        if (code.Length is < 2 or > 32 || code.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_')))
        {
            throw new IdentityValidationException("Organization code must be 2-32 letters, numbers, '-' or '_'.");
        }

        return code;
    }

    public static string RequireDisplayName(string? value)
    {
        var name = (value ?? "").Trim().Normalize(NormalizationForm.FormKC);
        if (name.Length is < 1 or > 200)
        {
            throw new IdentityValidationException("Display name must be between 1 and 200 characters.");
        }

        return name;
    }

    public static string RequirePassword(string? value)
    {
        if (value is null || value.Length is < 12 or > 256 || value.Any(char.IsControl))
        {
            throw new IdentityValidationException("Password must be between 12 and 256 characters.");
        }

        return value;
    }

    public static string NormalizeRoleName(string? value)
    {
        var name = (value ?? "").Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        if (name.Length is < 2 or > 64 || name.Any(char.IsControl))
        {
            throw new IdentityValidationException("Role name must be between 2 and 64 characters.");
        }

        return name;
    }

    public static string[] RequirePermissions(IEnumerable<string>? values,
        IReadOnlySet<string> allowedPermissions)
    {
        var permissions = (values ?? [])
            .Select(value => (value ?? "").Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (permissions.Length > 100)
        {
            throw new IdentityValidationException("A role can contain at most 100 permissions.");
        }

        if (permissions.Any(permission => !allowedPermissions.Contains(permission)))
        {
            throw new IdentityValidationException("The role contains an unknown permission.");
        }

        return permissions;
    }
}

public sealed class IdentityValidationException(string message) : Exception(message);
