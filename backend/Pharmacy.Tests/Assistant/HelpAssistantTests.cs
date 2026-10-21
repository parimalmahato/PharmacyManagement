using System.Net;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pharmacy.Application.Assistant;
using Pharmacy.Infrastructure.Assistant;
using Pharmacy.Api.Controllers;

namespace Pharmacy.Tests.Assistant;

public sealed class HelpAssistantTests
{
    [Fact]
    public async Task AcceptsUnmappedJwtSubjectClaimUsedByApplication()
    {
        var controller = new HelpAssistantController(new HelpAssistant(new GuideKnowledge(), new FakeGenerator()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "employee-1"), new Claim("role", "Cashier")], "test"))
                }
            }
        };
        var result = await controller.Ask(new HelpAssistantController.QuestionRequest { Question = "split payment" }, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task RejectsMissingAccountIdentity()
    {
        var controller = new HelpAssistantController(new HelpAssistant(new GuideKnowledge(), new FakeGenerator()))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = await controller.Ask(new HelpAssistantController.QuestionRequest { Question = "payment" }, default);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Theory]
    [InlineData("How do I record a split payment?", "9.")]
    [InlineData("How do I return medicines safely?", "10.")]
    [InlineData("How do I cancel a sale?", "11.")]
    [InlineData("forgot password", "3.")]
    public void RetrievesRelevantPackagedGuide(string question, string titlePrefix)
    {
        var sources = new GuideKnowledge().Search(question);
        Assert.Contains(sources, s => s.Title.StartsWith(titlePrefix));
        Assert.InRange(sources.Count, 1, 3);
    }

    [Fact]
    public async Task NoMatchDoesNotCallGenerator()
    {
        var generator = new FakeGenerator();
        var answer = await new HelpAssistant(new GuideKnowledge(), generator).AskAsync("zyxwvuq", default);
        Assert.Equal("no-match", answer.Mode);
        Assert.Empty(answer.Sources);
        Assert.Equal(0, generator.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsEmptyQuestions(string question) =>
        await Assert.ThrowsAsync<ArgumentException>(() => new HelpAssistant(new GuideKnowledge(), new FakeGenerator()).AskAsync(question, default));

    [Fact]
    public async Task RejectsOversizedQuestions() =>
        await Assert.ThrowsAsync<ArgumentException>(() => new HelpAssistant(new GuideKnowledge(), new FakeGenerator()).AskAsync(new string('x', 1001), default));

    [Fact]
    public async Task DisabledGenerationReturnsSourcesWithHonestMode()
    {
        var answer = await new HelpAssistant(new GuideKnowledge(), new FakeGenerator()).AskAsync("split payment", default);
        Assert.Equal("guide", answer.Mode);
        Assert.NotEmpty(answer.Sources);
    }

    [Fact]
    public async Task LangflowUsesPrivateKeyUniqueSessionsAndGroundedPrompt()
    {
        var handler = new Handler(HttpStatusCode.OK, """
            {"outputs":[{"outputs":[{"results":{"message":{"text":"Use Record payment [guide-9]."}}}]}]}
            """);
        var generator = Generator(handler);
        var sources = new[] { new HelpSource("guide-9", "Payments", "Record the actual receipt.") };
        Assert.Equal("Use Record payment [guide-9].", await generator.GenerateAsync("How to pay?", sources, default));
        await generator.GenerateAsync("How to pay?", sources, default);
        Assert.All(handler.Keys, key => Assert.Equal("test-secret", key));
        Assert.NotEqual(handler.Bodies[0], handler.Bodies[1]);
        Assert.Contains("Record the actual receipt.", handler.Bodies[0]);
        Assert.Contains("/api/v1/run/", handler.Uri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "provider failed")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"outputs\":{}}")]
    public async Task ProviderFailuresFallBackWithoutExposingResponse(HttpStatusCode status, string response)
    {
        var answer = await new HelpAssistant(new GuideKnowledge(), Generator(new Handler(status, response))).AskAsync("split payment", default);
        Assert.Equal("guide", answer.Mode);
        Assert.NotEmpty(answer.Sources);
        Assert.DoesNotContain(response, answer.Answer);
    }

    [Fact]
    public async Task DisabledProviderMakesNoNetworkCalls()
    {
        var handler = new Handler(HttpStatusCode.OK, "{}");
        var generator = new LangflowHelpGenerator(new HttpClient(handler), Options.Create(new HelpAssistantOptions()), NullLogger<LangflowHelpGenerator>.Instance);
        Assert.Null(await generator.GenerateAsync("test", [], default));
        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task OversizedProviderResponseFallsBack()
    {
        var handler = new Handler(HttpStatusCode.OK, new string('x', 256_001));
        Assert.Null(await Generator(handler).GenerateAsync("test", [], default));
    }

    [Fact]
    public async Task RejectsRemoteUnencryptedProviderWithoutSendingCredentials()
    {
        var handler = new Handler(HttpStatusCode.OK, "{}");
        var generator = new LangflowHelpGenerator(new HttpClient(handler), Options.Create(new HelpAssistantOptions
        {
            EnableLangflow = true, BaseUrl = "http://example.com/", FlowId = Guid.NewGuid().ToString(), ApiKey = "test-secret"
        }), NullLogger<LangflowHelpGenerator>.Instance);
        Assert.Null(await generator.GenerateAsync("test", [], default));
        Assert.Empty(handler.Bodies);
    }

    private static LangflowHelpGenerator Generator(Handler handler) => new(new HttpClient(handler), Options.Create(new HelpAssistantOptions
    {
        EnableLangflow = true, BaseUrl = "http://localhost:7860/", FlowId = "c67daa75-de7d-4d2a-9724-e02df86a5362", ApiKey = "test-secret"
    }), NullLogger<LangflowHelpGenerator>.Instance);

    private sealed class FakeGenerator : IHelpGenerator
    {
        public int Calls { get; private set; }
        public Task<string?> GenerateAsync(string question, IReadOnlyList<HelpSource> sources, CancellationToken ct)
        { Calls++; return Task.FromResult<string?>(null); }
    }

    private sealed class Handler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public List<string> Keys { get; } = [];
        public Uri? Uri { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            Keys.Add(request.Headers.GetValues("x-api-key").Single());
            Uri = request.RequestUri;
            return new(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
