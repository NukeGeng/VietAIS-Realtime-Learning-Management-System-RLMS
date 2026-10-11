using System.Text;
using LearningPlatform.Assessment.Domain;

namespace LearningPlatform.UnitTests;

public sealed class EssayRulesTests
{
    [Fact]
    public void Topic_normalization_collapses_layout_whitespace_and_case()
    {
        Assert.Equal("REALTIME LEARNING SYSTEMS",
            EssayRules.NormalizeTopic("  Realtime\n\tLearning   Systems  "));
        Assert.Equal("Realtime Learning Systems",
            EssayRules.CanonicalizeTitle("  Realtime\n\tLearning   Systems  "));
    }

    [Fact]
    public void Validation_policy_rejects_empty_required_sections()
    {
        var policy = new ReportValidationPolicy { RequiredSections = ["Introduction", " "] };

        Assert.Throws<AssessmentValidationException>(() => EssayRules.ValidatePolicy(policy));
    }

    [Fact]
    public async Task Pdf_extraction_returns_title_page_and_content_counts()
    {
        var bytes = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj << /Type /Page >> endobj\n" +
            "BT (Realtime Learning Systems) Tj ET\n" +
            "BT (Introduction) Tj ET\nBT (Conclusion) Tj ET\n%%EOF");

        await using var stream = new MemoryStream(bytes);
        var report = await EssayRules.ExtractAsync(stream, "application/pdf", CancellationToken.None);

        Assert.Equal("Realtime Learning Systems", report.Title);
        Assert.Equal(1, report.PageCount);
        Assert.Equal(5, report.WordCount);
        Assert.True(report.CharacterCount > 20);
    }

    [Fact]
    public void Question_order_is_a_stable_per_attempt_permutation()
    {
        var questions = Enumerable.Range(0, 8)
            .Select(index => new ActivityQuestion
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
                Order = index
            })
            .ToArray();
        var attemptId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var first = AssessmentRules.QuestionOrder(questions, true, attemptId);
        var second = AssessmentRules.QuestionOrder(questions, true, attemptId);

        Assert.Equal(questions.Length, first.Length);
        Assert.Equal(first, second);
        Assert.Equal(questions.Select(question => question.Id).Order(), first.Order());
        Assert.False(questions.Select(question => question.Id).SequenceEqual(first));
    }

    [Fact]
    public void Results_release_policy_hides_future_exam_results()
    {
        var activity = new AssessmentActivity
        {
            Kind = AssessmentActivityKind.Exam,
            ExamPolicy = new ExamPolicy { ResultsReleaseAt = DateTimeOffset.UtcNow.AddMinutes(1) }
        };

        Assert.False(AssessmentRules.IsResultsReleased(activity, DateTimeOffset.UtcNow));
        Assert.True(AssessmentRules.IsResultsReleased(activity, DateTimeOffset.UtcNow.AddMinutes(2)));
    }
}
