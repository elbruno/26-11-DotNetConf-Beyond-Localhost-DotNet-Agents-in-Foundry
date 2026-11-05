/*
 * Purpose: Test the remote caller without a Foundry endpoint or real credentials.
 * Verify final completion, token audience, HTTP routing, and typed result parsing.
 */
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using SupportRoutingCaller;
using Xunit;

namespace SupportRoutingTests;

public sealed class ResponsesAgentClientTests
{
    [Fact]
    public async Task SendsFoundryTokenAndReturnsTypedCompletedDecision()
    {
        var credential = new FixtureCredential();
        using var handler = new FixtureHandler(HttpStatusCode.OK, """
            {
              "status": "completed",
              "output": [{
                "type": "message",
                "content": [{
                  "type": "output_text",
                  "text": "{\"team\":\"billing\",\"confidence\":0.61,\"probabilities\":{\"billing\":0.7,\"technical-support\":0.1,\"account-access\":0.1,\"manual-review\":0.1},\"usage\":{\"inputTokens\":12,\"outputTokens\":0,\"totalTokens\":null,\"reasoningTokens\":null,\"cachedTokens\":null},\"answer\":\"Suggested team: billing. A person should confirm the route before taking action.\"}"
                }]
              }]
            }
            """);
        using var http = new HttpClient(handler);
        var client = new ResponsesAgentClient(
            http,
            credential,
            new Uri("https://example.services.ai.azure.com/agents/netconf-support-routing/protocols/openai/"),
            "netconf-support-routing");

        var result = await client.InvokeAsync("synthetic duplicate charge");

        Assert.Equal("https://example.services.ai.azure.com/agents/netconf-support-routing/protocols/openai/responses?api-version=v1", handler.Url);
        Assert.Equal("https://ai.azure.com/.default", credential.Scope);
        Assert.Equal("Bearer synthetic-access-token", handler.Authorization);
        Assert.False(handler.HasApiKey);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("netconf-support-routing", request.RootElement.GetProperty("model").GetString());
        Assert.Equal("synthetic duplicate charge", request.RootElement.GetProperty("input").GetString());
        Assert.False(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(request.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("billing", result.Team);
        Assert.Equal(.61, result.Confidence);
        Assert.Equal(0, result.Usage!.OutputTokens);
        Assert.Null(result.Usage.TotalTokens);
    }

    [Theory]
    [InlineData(
        "https://example.services.ai.azure.com/agents/netconf-support-routing/protocols/openai",
        "/agents/netconf-support-routing/protocols/openai/responses",
        "?api-version=v1")]
    [InlineData(
        "https://example.services.ai.azure.com/agents/netconf-support-routing/protocols/openai/?mode=demo&region=west",
        "/agents/netconf-support-routing/protocols/openai/responses",
        "?mode=demo&region=west&api-version=v1")]
    [InlineData(
        "https://example.services.ai.azure.com/agents/netconf-support-routing/protocols/openai?api-version=preview&session_id=sample",
        "/agents/netconf-support-routing/protocols/openai/responses",
        "?session_id=sample&api-version=v1")]
    [InlineData(
        "https://example.services.ai.azure.com/agents/team%20east/protocols/openai?filter=a%2Bb",
        "/agents/team%20east/protocols/openai/responses",
        "?filter=a%2Bb&api-version=v1")]
    public async Task AddsRequiredVersionWhilePreservingPathAndOtherQuery(
        string baseUrl,
        string expectedPath,
        string expectedQuery)
    {
        using var handler = new FixtureHandler(HttpStatusCode.OK, """
            {
              "status": "completed",
              "output": [{
                "type": "message",
                "content": [{
                  "type": "output_text",
                  "text": "{\"team\":\"billing\",\"confidence\":0.61,\"probabilities\":{\"billing\":0.7,\"technical-support\":0.1,\"account-access\":0.1,\"manual-review\":0.1},\"answer\":\"Suggested team: billing.\"}"
                }]
              }]
            }
            """);
        using var http = new HttpClient(handler);
        var client = new ResponsesAgentClient(
            http,
            new FixtureCredential(),
            new Uri(baseUrl),
            "netconf-support-routing");

        var result = await client.InvokeAsync("synthetic ticket");

        var requestUri = new Uri(handler.Url!);
        Assert.Equal(expectedPath, requestUri.AbsolutePath);
        Assert.Equal(expectedQuery, requestUri.Query);
        Assert.Equal("billing", result.Team);
    }

    [Fact]
    public async Task FailedResponsesStatusIsNotDeserializedAsSuccess()
    {
        using var handler = new FixtureHandler(
            HttpStatusCode.OK,
            """{"status":"failed","error":{"message":"Synthetic provider failure"}}""");
        using var http = new HttpClient(handler);
        var client = Create(http, new FixtureCredential());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.InvokeAsync("synthetic ticket"));

        Assert.Contains("Final status: failed", error.Message);
    }

    [Fact]
    public async Task HttpAuthorizationFailureRemainsVisible()
    {
        using var handler = new FixtureHandler(HttpStatusCode.Forbidden, """{"error":"forbidden"}""");
        using var http = new HttpClient(handler);
        var client = Create(http, new FixtureCredential());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.InvokeAsync("synthetic ticket"));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task MissingCompletedOutputIsNotAValidDecision()
    {
        using var handler = new FixtureHandler(HttpStatusCode.OK, """{"status":"completed","output":[]}""");
        using var http = new HttpClient(handler);
        var client = Create(http, new FixtureCredential());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.InvokeAsync("synthetic ticket"));
    }

    private static ResponsesAgentClient Create(HttpClient http, TokenCredential credential) =>
        new(
            http,
            credential,
            new Uri("https://example.services.ai.azure.com/agents/netconf-support-routing/protocols/openai/"),
            "netconf-support-routing");

    private sealed class FixtureCredential : TokenCredential
    {
        public string? Scope { get; private set; }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            Scope = Assert.Single(requestContext.Scopes);
            return new AccessToken("synthetic-access-token", DateTimeOffset.UtcNow.AddMinutes(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    private sealed class FixtureHandler(HttpStatusCode status, string content) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }
        public bool HasApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            HasApiKey = request.Headers.Contains("api-key");
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }
}
