using System.Text.RegularExpressions;
using Pharmacy.Application.Assistant;

namespace Pharmacy.Infrastructure.Assistant;

// Only this reviewed, embedded user guide is indexed. No arbitrary file paths or database access.
/// <summary>
/// Loads numbered sections from the embedded user guide and ranks keyword matches locally without accessing business records.
/// </summary>
public sealed partial class GuideKnowledge : IHelpKnowledge
{
    private readonly HelpSource[] sections;
    /// <summary>
    /// Approved packaged sections used for indexing and trusted source-ID resolution; external vector payload text is not used as guide content.
    /// </summary>
    public IReadOnlyList<HelpSource> All => sections;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "the", "and", "for", "how", "can", "you", "are", "with", "this", "that", "what", "does", "from", "have", "please", "application", "about" };

    public GuideKnowledge()
    {
        using var stream = typeof(GuideKnowledge).Assembly.GetManifestResourceStream("Pharmacy.UserGuide")
            ?? throw new InvalidOperationException("The application user guide was not packaged.");
        using var reader = new StreamReader(stream);
        var parts = Regex.Split(reader.ReadToEnd(), @"(?m)^## ");
        sections = parts.Skip(1).Where(p => Regex.IsMatch(p, @"^\d+\. ")).Select((p, index) =>
        {
            var newline = p.IndexOf('\n');
            return new HelpSource($"guide-{index + 1}", p[..newline].Trim(), p[(newline + 1)..].Trim());
        }).ToArray();
    }

    private static string Normalize(string word) => word.ToLowerInvariant() switch
    {
        "cancelled" or "cancelling" or "cancellation" => "cancel",
        "refunding" or "refunds" => "refund",
        "payments" or "paying" or "paid" or "pay" => "payment",
        "passwords" => "password",
        "forgotten" or "forgot" or "forget" => "forgot",
        "medicines" => "medicine",
        "returns" or "returning" or "restocking" or "restock" => "return",
        "employees" or "onboarding" or "onboard" => "employee",
        "invoices" or "billing" => "invoice",
        "purchases" or "purchasing" => "purchase",
        var value => value
    };

    private static HashSet<string> Tokens(string text) => Words().Matches(text).Select(m => Normalize(m.Value))
        .Where(w => w.Length > 2 && !StopWords.Contains(w)).ToHashSet();

    /// <summary>
    /// Normalizes common workflow terms and returns up to three keyword-ranked sections from the packaged user guide.
    /// </summary>
    public IReadOnlyList<HelpSource> Search(string question)
    {
        var terms = Tokens(question);
        if (terms.Count == 0) return [];
        return sections.Select(s =>
        {
            var title = Tokens(s.Title);
            var body = Tokens(s.Content);
            var matched = terms.Count(body.Contains);
            return (Source: s, Score: terms.Count(title.Contains) * 4 + matched, Matched: matched);
        }).Where(x => x.Score >= 2 && x.Matched >= Math.Min(2, terms.Count))
          .OrderByDescending(x => x.Score).Take(3).Select(x => x.Source).ToArray();
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}
