using LearningPlatform.Identity.Domain;

namespace LearningPlatform.UnitTests;

public sealed class IdentityRulesTests
{
    [Fact]
    public void Email_is_normalized_for_lookup_without_changing_the_original_value()
    {
        Assert.Equal("USER@EXAMPLE.COM", IdentityRules.NormalizeEmail("  user@example.com "));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("user\r@example.com")]
    [InlineData("")]
    public void Invalid_email_is_rejected(string value)
    {
        Assert.Throws<IdentityValidationException>(() => IdentityRules.NormalizeEmail(value));
    }

    [Fact]
    public void Organization_code_and_role_names_are_canonicalized()
    {
        Assert.Equal("CENTER_01", IdentityRules.NormalizeCode(" center_01 "));
        Assert.Equal("TEACHER", IdentityRules.NormalizeRoleName(" teacher "));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("INVALID CODE")]
    public void Invalid_organization_code_is_rejected(string value)
    {
        Assert.Throws<IdentityValidationException>(() => IdentityRules.NormalizeCode(value));
    }

    [Fact]
    public void Password_policy_rejects_short_or_control_input()
    {
        Assert.Throws<IdentityValidationException>(() => IdentityRules.RequirePassword("too short"));
        Assert.Throws<IdentityValidationException>(() =>
            IdentityRules.RequirePassword("correct\nhorse battery staple"));
    }
}
