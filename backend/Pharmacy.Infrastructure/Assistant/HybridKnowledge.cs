using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Assistant;

namespace Pharmacy.Infrastructure.Assistant;

/// <summary>
/// Attempts semantic guide retrieval when configured, falling back to local keyword retrieval on empty results or supported provider failures.
/// </summary>
public sealed class HybridKnowledge(GuideKnowledge guide, ISemanticKnowledge semantic, IOptions<AiOptions> options,
    ILogger<HybridKnowledge> logger) : IHelpKnowledge
{
    /// <summary>
    /// The retrieval strategy that supplied the last successful result in this scoped instance, rather than merely the configured preference.
    /// </summary>
    public string RetrievalMode { get; private set; } = "keyword";
    /// <summary>
    /// Performs synchronous local keyword retrieval without making external service calls.
    /// </summary>
    public IReadOnlyList<HelpSource> Search(string question) => guide.Search(question);
    /// <summary>
    /// Attempts semantic retrieval first and uses keyword retrieval when no matches or supported provider failures occur; records the retrieval mode that actually succeeded.
    /// </summary>
    public async Task<IReadOnlyList<HelpSource>> SearchAsync(string question, CancellationToken ct)
    {
        RetrievalMode = "keyword";
        if (options.Value.EnableVectorSearch)
        {
            try { var sources = await semantic.SearchAsync(question, ct); if (sources.Count > 0) { RetrievalMode = "vector"; return sources; } }
            catch (Exception ex) when (ex is AiUnavailableException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
            { logger.LogWarning("Semantic help unavailable ({Failure}); using keyword retrieval.", ex.GetType().Name); }
        }
        return guide.Search(question);
    }
}
