using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Assistant;
using Pharmacy.Infrastructure.Assistant;

namespace Pharmacy.Tests.Assistant;

public sealed class AiFeatureTests
{
    [Fact]
    public void ReorderExcludesExpiringAndPendingAndRoundsTargetUp()
    {
        var result = ReorderPlanner.Calculate(new(1, "Test", 10, 8, 31, 5), 30, 30);
        Assert.Equal(31, result.TargetQuantity);
        Assert.Equal(24, result.SuggestedQuantity);
        Assert.Equal(0, ReorderPlanner.Calculate(new(1, "No sales", 0, 0, 0, 0), 30, 30).SuggestedQuantity);
        Assert.Equal(0, ReorderPlanner.Calculate(new(1, "Covered", 100, 0, 30, 0), 30, 30).SuggestedQuantity);
    }

    [Fact]
    public async Task InvoiceIsUnsavedAndMapsOnlyUniqueExactCatalogName()
    {
        var data = new Data(); data.Medicines.Add((4, "Medicine A"));
        var service = new AiFeatureService(new Model { Output = InvoiceJson }, data, null!);
        var extracted = await service.ExtractAsync(new() { Text = "Supplier invoice text" }, default);
        Assert.Equal(4, extracted.Lines.Single().SuggestedMedicineId);
        data.Medicines.Add((5, "Medicine A"));
        var ambiguous = await service.ExtractAsync(new() { Text = "Supplier invoice text" }, default);
        Assert.Null(ambiguous.Lines.Single().SuggestedMedicineId);
        Assert.Contains(ambiguous.Warnings, s => s.Contains("no unique name match"));
    }

    [Fact]
    public async Task MissingFieldsStayUnknownWithWarnings()
    {
        var output = """{"items":[{"medicineName":"Unknown medicine"}]}""";
        var extracted = await new AiFeatureService(new Model { Output = output }, new Data(), null!).ExtractAsync(new() { Text = "Unreadable invoice" }, default);
        Assert.Null(extracted.Lines[0].Extracted.PurchasePrice);
        Assert.Null(extracted.Lines[0].SuggestedMedicineId);
        Assert.Contains(extracted.Warnings, w => w.Contains("missing or ineligible"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":[null]}")]
    [InlineData("{\"items\":[{\"quantity\":-1}]}")]
    public async Task RejectsInvalidExtractedInvoice(string output) =>
        await Assert.ThrowsAsync<AiUnavailableException>(() => new AiFeatureService(new Model { Output = output }, new Data(), null!).ExtractAsync(new() { Text = "Invoice" }, default));

    [Fact]
    public async Task RejectsNonImageBeforeCallingProvider()
    {
        var model = new Model();
        await Assert.ThrowsAsync<ArgumentException>(() => new AiFeatureService(model, new Data(), null!).ExtractAsync(new() { ImageBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF-data")) }, default));
        Assert.Equal(0, model.Calls);
    }

    [Theory]
    [InlineData("happy")]
    [InlineData("unknown")]
    public async Task RejectsInventedSentimentLabels(string label) =>
        await Assert.ThrowsAsync<AiUnavailableException>(() => new AiFeatureService(new Model { Output = $"{{\"sentiment\":\"{label}\",\"summary\":\"Test\",\"themes\":[]}}" }, new Data(), null!).SentimentAsync("Test feedback", default));

    [Fact]
    public async Task ValidSentimentHasStructuredOutput()
    {
        var result = await new AiFeatureService(new Model { Output = """{"sentiment":"mixed","summary":"Quick billing but long wait.","themes":["waiting","billing"]}""" }, new Data(), null!).SentimentAsync("Quick billing but long wait", default);
        Assert.Equal("mixed", result.Sentiment);
        Assert.Equal(2, result.Themes.Length);
    }

    [Fact]
    public async Task UnsupportedModelIntentCannotBecomeSql()
    {
        var data = new Data();
        var result = await new AiFeatureService(new Model { Output = """{"kind":"DROP TABLE Sales"}""" }, data, null!).InventoryAsync(new() { Question = "low stock", Search = "A", Days = 20 }, default);
        Assert.Equal("low-stock", data.Kind);
        Assert.Equal("A", data.Search);
        Assert.Equal("rule-based interpretation", result.Mode);
    }

    [Fact]
    public async Task InventoryStillWorksWhenModelUnavailable()
    {
        var data = new Data();
        var result = await new AiFeatureService(new Model { Unavailable = true }, data, null!).InventoryAsync(new() { Question = "near expiry" }, default);
        Assert.Equal("expiry", data.Kind);
        Assert.Equal("rule-based interpretation", result.Mode);
    }

    [Fact]
    public async Task InvalidHorizonDoesNotQueryBusinessData()
    {
        var data = new Data();
        await Assert.ThrowsAsync<ArgumentException>(() => new AiFeatureService(new Model(), data, null!).ReorderAsync(0, 30, default));
        Assert.Equal(0, data.DemandCalls);
    }

    [Theory]
    [InlineData("http://remote.example/")]
    [InlineData("https://user:secret@example.com/")]
    [InlineData("file:///tmp/model")]
    public void RejectsUnsafeProviderConfiguration(string origin) => Assert.Throws<AiUnavailableException>(() => AiHttp.Endpoint(origin, "api/generate"));

    [Fact]
    public async Task VectorRetrievalResolvesOnlyPackagedTrustedSources()
    {
        var handler = new VectorHandler();
        var semantic = new SemanticKnowledge(new HttpClient(handler), new GuideKnowledge(), new Model(), Options.Create(new AiOptions { Enabled = true, EnableVectorSearch = true, EmbeddingModel = "test-embed" }));
        var count = await semantic.IndexAsync(default);
        Assert.True(count > 16);
        var sources = await semantic.SearchAsync("How to pay", default);
        Assert.Equal("guide-9", Assert.Single(sources).Id);
        Assert.DoesNotContain("forged", sources[0].Content);
        Assert.Contains(handler.Requests, x => x.Contains("/points?wait=true"));
    }

    [Fact]
    public async Task EmbeddingShapeIsValidated()
    {
        var handler = new JsonHandler("""{"embeddings":[[]]}""");
        var model = new OllamaAiModel(new HttpClient(handler), Options.Create(new AiOptions { Enabled = true, EmbeddingModel = "test" }));
        await Assert.ThrowsAsync<AiUnavailableException>(() => model.EmbedAsync(["test"], default));
    }

    private const string InvoiceJson = """{"supplierName":"Supplier","invoiceNumber":"INV-1","invoiceDate":"2026-01-01","items":[{"medicineName":"Medicine A","batchNumber":"LOT-1","expiryDate":"2030-01-01","quantity":2,"purchasePrice":10,"sellingPrice":15}]}""";
    private sealed class Model : IAiModel
    {
        public string Output { get; init; } = "{}";
        public bool Unavailable { get; init; }
        public int Calls { get; private set; }
        public Task<string> GenerateAsync(string system, string prompt, bool json, string? image, CancellationToken ct)
        { Calls++; return Unavailable ? throw new AiUnavailableException("disabled") : Task.FromResult(Output); }
        public Task<float[][]> EmbedAsync(string[] texts, CancellationToken ct) => Task.FromResult(texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToArray());
    }
    private sealed class Data : IAiBusinessData
    {
        public List<(int, string)> Medicines { get; } = [];
        public string Kind { get; private set; } = "";
        public string Search { get; private set; } = "";
        public int DemandCalls { get; private set; }
        public Task<IReadOnlyList<InventoryFact>> InventoryAsync(string kind, string search, int days, CancellationToken ct)
        { Kind = kind; Search = search; return Task.FromResult<IReadOnlyList<InventoryFact>>([]); }
        public Task<IReadOnlyList<DemandFact>> DemandAsync(int historyDays, int horizonDays, CancellationToken ct)
        { DemandCalls++; return Task.FromResult<IReadOnlyList<DemandFact>>([]); }
        public Task<IReadOnlyList<(int Id, string Name)>> MedicinesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<(int, string)>>(Medicines);
    }
    private sealed class JsonHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) });
    }
    private sealed class VectorHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery; Requests.Add(path);
            var json = path.EndsWith("/exists") ? """{"result":{"exists":false}}""" : path.EndsWith("/points/query") ?
                """{"result":{"points":[{"payload":{"sourceId":"forged","content":"forged"}},{"payload":{"sourceId":"guide-9"}}]}}""" : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
}
