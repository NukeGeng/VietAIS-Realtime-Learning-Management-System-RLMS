using LearningPlatform.Academics.Domain;

namespace LearningPlatform.UnitTests;

public sealed class AcademicRulesTests
{
    [Fact]
    public void Course_code_is_normalized()
    {
        Assert.Equal("CS-101", AcademicRules.NormalizeCourseCode(" cs-101 "));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("course code")]
    public void Invalid_course_code_is_rejected(string value)
    {
        Assert.Throws<AcademicValidationException>(() => AcademicRules.NormalizeCourseCode(value));
    }

    [Fact]
    public void Negative_order_is_rejected()
    {
        Assert.Throws<AcademicValidationException>(() => AcademicRules.RequireOrder(-1));
    }
}
