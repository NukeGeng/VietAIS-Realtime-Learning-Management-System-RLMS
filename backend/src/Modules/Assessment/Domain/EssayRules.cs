using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace LearningPlatform.Assessment.Domain;

public static class EssayRules
{
    public const int MaxTitleLength = 300;
    public const int MaxDescriptionLength = 20_000;
    public const int MaxSectionLength = 200;

    public static string RequireTitle(string? value)
    {
        var title = value?.Trim() ?? "";
        if (title.Length == 0 || title.Length > MaxTitleLength)
        {
            throw new AssessmentValidationException("The essay title is required and must be at most 300 characters.");
        }

        return title;
    }

    public static string NormalizeTopic(string? value)
    {
        return CanonicalizeTitle(value).ToUpperInvariant();
    }

    public static string CanonicalizeTitle(string? value) =>
        Regex.Replace(value?.Trim() ?? "", @"\s+", " ").Normalize(NormalizationForm.FormKC);

    public static void ValidatePolicy(ReportValidationPolicy policy)
    {
        var allowedFileTypes = policy.AllowedFileTypes ?? [];
        var requiredSections = policy.RequiredSections ?? [];
        if (requiredSections.Any(section => string.IsNullOrWhiteSpace(section) ||
                section.Trim().Length > MaxSectionLength))
        {
            throw new AssessmentValidationException(
                "Required section names must be non-empty and at most 200 characters.");
        }

        policy.AllowedFileTypes = allowedFileTypes
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        policy.RequiredSections = requiredSections
            .Select(CanonicalizeTitle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (policy.MaxFileBytes <= 0 || policy.MaxFileBytes > 250 * 1024 * 1024 ||
            policy.MinimumPages is < 1 || policy.MinimumWords is < 1 ||
            policy.MinimumCharacters is < 1 || policy.RequiredSections.Length > 100 ||
            policy.AllowedFileTypes.Length == 0)
        {
            throw new AssessmentValidationException("The essay validation policy contains an invalid threshold.");
        }

    }

    public static async Task<ExtractedReport> ExtractAsync(Stream content, string contentType,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        string text;
        int? pages = null;
        if (contentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                StringComparison.OrdinalIgnoreCase))
        {
            text = ExtractDocx(bytes);
            pages = null;
        }
        else if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            text = ExtractPdf(bytes);
            pages = CountPdfPages(text, bytes);
        }
        else
        {
            throw new AssessmentValidationException("Only PDF and DOCX reports are supported.");
        }

        var title = FirstLine(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        var words = text.Length == 0 ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return new ExtractedReport(text, title, pages, words, text.Length, bytes);
    }

    private static string ExtractDocx(byte[] bytes)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException();
            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd();
            xml = Regex.Replace(xml, @"<w:(?:tab|br)[^>]*/>", " ", RegexOptions.IgnoreCase);
            xml = Regex.Replace(xml, @"</w:p>", Environment.NewLine, RegexOptions.IgnoreCase);
            return Regex.Replace(System.Net.WebUtility.HtmlDecode(
                Regex.Replace(xml, "<[^>]+>", " ")), @"[ \t]+", " ");
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
        {
            throw new AssessmentValidationException("The DOCX report cannot be read.");
        }
    }

    private static string ExtractPdf(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var matches = Regex.Matches(raw, @"\((?<text>(?:\\.|[^)])*)\)\s*T[Jj]");
        if (matches.Count == 0)
        {
            return "";
        }

        return string.Join(Environment.NewLine,
            matches.Select(match => Regex.Unescape(match.Groups["text"].Value)));
    }

    private static int CountPdfPages(string _, byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var count = Regex.Matches(raw, @"/Type\s*/Page(?:\s|/|>)", RegexOptions.IgnoreCase).Count;
        return Math.Max(count, 1);
    }

    private static string? FirstLine(string text) => text.Split(["\r\n", "\n", "\r"],
            StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value.Trim())
        .FirstOrDefault(value => value.Length > 0);
}

public sealed record ExtractedReport(string Text, string? Title, int? PageCount, int WordCount,
    int CharacterCount, byte[] Bytes);
