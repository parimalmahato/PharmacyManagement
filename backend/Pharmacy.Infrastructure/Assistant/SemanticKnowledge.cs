using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Assistant;

namespace Pharmacy.Infrastructure.Assistant;

/// <summary>
/// Embeds the packaged user guide into a content-addressed Qdrant collection and resolves vector matches back to trusted guide sections.
/// </summary>
/// <remarks>
/// The collection identity includes guide content and the embedding-model name. Rebuild and index after
/// changing the guide; use a new name for changed model weights. Retired collections are not deleted here.
/// </remarks>
public sealed class SemanticKnowledge(HttpClient client, GuideKnowledge guide, IAiModel model, IOptions<AiOptions> settings) : ISemanticKnowledge
{
    private string Collection => "pharmacy_help_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        settings.Value.EmbeddingModel + string.Join("", guide.All.Select(x => x.Id + x.Title + x.Content)))))[..20].ToLowerInvariant();

    /// <summary>
    /// Adds only the configured server-side Qdrant key and sends a bounded request when vector search is enabled.
    /// </summary>
    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? data, CancellationToken ct)
    {
        if (!settings.Value.EnableVectorSearch) throw new AiUnavailableException("Vector search is disabled.");
        using var request = new HttpRequestMessage(method, AiHttp.Endpoint(settings.Value.QdrantUrl, path));
        if (!string.IsNullOrWhiteSpace(settings.Value.QdrantApiKey)) request.Headers.Add("api-key", settings.Value.QdrantApiKey);
        if (data is not null) request.Content = JsonContent.Create(data);
        return await AiHttp.SendAsync(client, request, ct);
    }

    /// <summary>
    /// Chunks the current guide, embeds it in bounded batches and upserts stable point IDs into a guide-and-model-specific collection; no business records are indexed.
    /// </summary>
    /// <param name="ct">Cancels embedding requests and vector-store writes.</param>
    /// <returns>The number of guide chunks submitted in the completed upsert.</returns>
    /// <exception cref="AiUnavailableException">Embedding or vector services are disabled, unreachable or return unusable data.</exception>
    public async Task<int> IndexAsync(CancellationToken ct)
    {
        // Content-addressed collection: changed documents/models never retrieve stale vectors.
        var chunks = guide.All.SelectMany(s => Enumerable.Range(0, (s.Content.Length + 1399) / 1400)
            .Select(i => new { Source = s, Text = s.Title + "\n" + s.Content.Substring(i * 1400, Math.Min(1400, s.Content.Length - i * 1400)) })).ToArray();
        var vectors = new List<float[]>();
        foreach (var batch in chunks.Chunk(8)) vectors.AddRange(await model.EmbedAsync(batch.Select(c => c.Text).ToArray(), ct));
        using var exists = await SendAsync(HttpMethod.Get, $"collections/{Collection}/exists", null, ct);
        if (!exists.RootElement.GetProperty("result").GetProperty("exists").GetBoolean())
        {
            using var created = await SendAsync(HttpMethod.Put, $"collections/{Collection}", new { vectors = new { size = vectors[0].Length, distance = "Cosine" } }, ct);
        }
        // One synchronous upsert; reruns use stable numeric IDs. Only server-owned guide IDs enter payloads.
        using var saved = await SendAsync(HttpMethod.Put, $"collections/{Collection}/points?wait=true", new
        {
            points = chunks.Select((chunk, i) => new { id = i + 1, vector = vectors[i], payload = new { sourceId = chunk.Source.Id } })
        }, ct);
        return chunks.Length;
    }

    /// <summary>
    /// Embeds a question, queries Qdrant and resolves distinct matched source IDs against the packaged guide instead of trusting external payload text.
    /// </summary>
    /// <param name="question">The bounded application-help question to embed.</param>
    /// <param name="ct">Cancels provider requests.</param>
    /// <returns>Up to three trusted guide sections in match order, or an empty list when no trusted match meets the threshold.</returns>
    public async Task<IReadOnlyList<HelpSource>> SearchAsync(string question, CancellationToken ct)
    {
        var vector = (await model.EmbedAsync([question], ct))[0];
        using var result = await SendAsync(HttpMethod.Post, $"collections/{Collection}/points/query", new
        { query = vector, limit = 8, with_payload = true, score_threshold = 0.35 }, ct);
        var trusted = guide.All.ToDictionary(s => s.Id);
        return result.RootElement.GetProperty("result").GetProperty("points").EnumerateArray()
            .Select(p => p.GetProperty("payload").GetProperty("sourceId").GetString())
            .Where(id => id is not null && trusted.ContainsKey(id)).Distinct().Take(3).Select(id => trusted[id!]).ToArray();
    }
}
