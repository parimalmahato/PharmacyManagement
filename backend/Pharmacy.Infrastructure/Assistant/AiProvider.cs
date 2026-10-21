using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Assistant;

namespace Pharmacy.Infrastructure.Assistant;

/// <summary>
/// Server-side settings for local or HTTPS model and vector services. Enabling a flag does not verify connectivity or installed models.
/// </summary>
public sealed class AiOptions
{
    /// <summary>
    /// Enables Ollama inference and embedding requests; disabled or incomplete configuration produces an explicit unavailable result.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Trusted server-configured Ollama origin; remote services require HTTPS and local development may use loopback HTTP.
    /// </summary>
    public string OllamaUrl { get; set; } = "http://localhost:11434/";
    /// <summary>
    /// Installed text-model name used for help generation, query interpretation, explanations, extraction from text and sentiment.
    /// </summary>
    public string Model { get; set; } = "";
    /// <summary>
    /// Installed image-capable model name; leaving it empty keeps image extraction unavailable.
    /// </summary>
    public string VisionModel { get; set; } = "";
    /// <summary>
    /// Installed embedding-model name included in the guide collection identity; change the name when changing embedding weights.
    /// </summary>
    public string EmbeddingModel { get; set; } = "";
    /// <summary>
    /// Allows Qdrant guide indexing and retrieval; successful use also requires a configured embedding model and completed index.
    /// </summary>
    public bool EnableVectorSearch { get; set; }
    /// <summary>
    /// Trusted Qdrant origin used only by the server-side semantic knowledge adapter.
    /// </summary>
    public string QdrantUrl { get; set; } = "http://localhost:6333/";
    /// <summary>
    /// Optional server-side vector-service secret that must not be exposed to frontend configuration or logs.
    /// </summary>
    public string QdrantApiKey { get; set; } = "";
}

/// <summary>
/// Builds trusted provider endpoints and bounds HTTP response sizes and execution deadlines without exposing response bodies to callers.
/// </summary>
public static class AiHttp
{
    /// <summary>
    /// Accepts an absolute HTTPS service URL or loopback HTTP URL and rejects URL credentials, queries and fragments before appending a fixed provider path.
    /// </summary>
    public static Uri Endpoint(string origin, string path)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new AiUnavailableException("AI service URL must use HTTPS, or HTTP on loopback for local development.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/" + path.TrimStart('/'));
    }

    /// <summary>
    /// Sends a provider request with a linked 90-second deadline and bounded streamed response; caller cancellation propagates and supported failures become a provider-unavailable error.
    /// </summary>
    /// <param name="client">The configured server HTTP client, with redirects disabled by dependency injection.</param>
    /// <param name="request">The caller-owned request containing only the intended provider payload and credentials.</param>
    /// <param name="ct">Caller cancellation, kept distinct from the adapter deadline.</param>
    /// <param name="limit">Maximum response bytes accepted before JSON parsing.</param>
    /// <returns>A parsed document that the caller must dispose.</returns>
    /// <exception cref="AiUnavailableException">The response fails, exceeds its size limit, times out or is invalid JSON.</exception>
    public static async Task<JsonDocument> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken ct, int limit = 2_000_000)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(90));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new AiUnavailableException($"AI service returned HTTP {(int)response.StatusCode}. Check its configuration and installed models.");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(bytes, deadline.Token)) != 0)
            {
                if (buffer.Length + count > limit) throw new AiUnavailableException("AI service response exceeded the allowed size.");
                buffer.Write(bytes, 0, count);
            }
            return JsonDocument.Parse(buffer.ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AiUnavailableException("AI service timed out. Try a smaller document or check the model server."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        { throw new AiUnavailableException("AI service is unavailable or returned an invalid response."); }
    }
}

/// <summary>
/// Implements text/vision generation and embeddings through Ollama&apos;s non-streaming HTTP APIs, validating provider response shapes before use.
/// </summary>
public sealed class OllamaAiModel(HttpClient client, IOptions<AiOptions> settings) : IAiModel
{
    /// <summary>
    /// Uses the configured text or vision model with non-streaming output and optional JSON format; validates response shape and length before returning untrusted generated text.
    /// </summary>
    /// <param name="system">Application-authored instructions describing the allowed task.</param>
    /// <param name="prompt">Question or source context; supplied documents remain untrusted data.</param>
    /// <param name="json">Whether to request JSON output; application field validation is still required.</param>
    /// <param name="image">Optional raw base64 page image; its presence selects the configured vision model.</param>
    /// <param name="ct">Cancels the provider request.</param>
    /// <returns>The model's generated text, not verified business facts.</returns>
    public async Task<string> GenerateAsync(string system, string prompt, bool json, string? image, CancellationToken ct)
    {
        var options = settings.Value;
        var model = image is null ? options.Model : options.VisionModel;
        if (!options.Enabled || string.IsNullOrWhiteSpace(model)) throw new AiUnavailableException("Configure Ai:Enabled and the required Ollama model before using this feature.");
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model, ["system"] = system, ["prompt"] = prompt, ["stream"] = false,
            ["options"] = new { temperature = 0, num_predict = 6000 }
        };
        if (json) payload["format"] = "json";
        if (image is not null) payload["images"] = new[] { image };
        using var request = new HttpRequestMessage(HttpMethod.Post, AiHttp.Endpoint(options.OllamaUrl, "api/generate")) { Content = JsonContent.Create(payload) };
        using var result = await AiHttp.SendAsync(client, request, ct);
        if (result.RootElement.ValueKind != JsonValueKind.Object || !result.RootElement.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.String ||
            response.GetString() is not { Length: > 0 and <= 50000 } text)
            throw new AiUnavailableException("The model returned no usable answer.");
        return text;
    }

    /// <summary>
    /// Embeds a batch without silently truncating source text and verifies vector count, dimensions and finite numeric values.
    /// </summary>
    /// <param name="texts">Input texts in the order expected by the indexer or query adapter.</param>
    /// <param name="ct">Cancels the provider request.</param>
    /// <returns>One same-dimension finite embedding per input text, preserving input order.</returns>
    public async Task<float[][]> EmbedAsync(string[] texts, CancellationToken ct)
    {
        var options = settings.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.EmbeddingModel)) throw new AiUnavailableException("Configure the Ollama embedding model first.");
        using var request = new HttpRequestMessage(HttpMethod.Post, AiHttp.Endpoint(options.OllamaUrl, "api/embed"))
        { Content = JsonContent.Create(new { model = options.EmbeddingModel, input = texts, truncate = false }) };
        using var result = await AiHttp.SendAsync(client, request, ct);
        float[][]? vectors;
        try { vectors = result.RootElement.ValueKind == JsonValueKind.Object && result.RootElement.TryGetProperty("embeddings", out var array) ? array.Deserialize<float[][]>() : null; }
        catch (JsonException) { throw new AiUnavailableException("Invalid embedding response."); }
        if (vectors is null || vectors.Length != texts.Length || vectors.Any(v => v.Length == 0 || v.Length > 8192 || v.Any(x => !float.IsFinite(x))) ||
            vectors.Any(v => v.Length != vectors[0].Length)) throw new AiUnavailableException("Invalid embedding response.");
        return vectors;
    }
}
