/*
 * Purpose: Verify the community client's native request contract with synthetic HTTP.
 * Fake credentials prove the expected audience without obtaining or storing a real token.
 */
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Extensions.Logging;
using SupportRouting;
using Xunit;

namespace SupportRoutingTests;

public sealed class NativeClientTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LocalHostUsesOnlyAzureCliCredential(string? hostingEnvironment) =>
        Assert.IsType<AzureCliCredential>(Program.CreateDecisionCredential(hostingEnvironment));

    [Fact]
    public void FoundryHostUsesDefaultAzureCredential() =>
        Assert.IsType<DefaultAzureCredential>(Program.CreateDecisionCredential("foundry"));

    [Fact]
    public async Task CallsNativeSystemOneWithExplicitCredential()
    {
        var credential = new FixtureCredential();
        using var handler = new FixtureHandler(
            HttpStatusCode.OK,
            """{"answers":{"decision":{"type":"choice","choice":"billing","confidence":0.61,"probabilities":{"billing":0.7,"technical-support":0.1,"account-access":0.1,"manual-review":0.1}}},"usage":{"input_tokens":12,"output_tokens":0}}""");
        using var http = new HttpClient(handler);
        using var client = Create(http, credential);

        var result = await client.ChooseAsync("synthetic", "Which team?", SupportRoutingAgent.Routes);

        Assert.Equal(
            "https://example.services.ai.azure.com/providers/microsoft/v1/systemone",
            handler.Url);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.False(handler.HasApiKey);
        Assert.Equal("https://cognitiveservices.azure.com/.default", credential.Scope);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("test-decision", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("synthetic", body.RootElement.GetProperty("state").GetString());
        Assert.Equal(
            "choice",
            body.RootElement.GetProperty("questions").GetProperty("decision").GetProperty("type").GetString());
        Assert.Equal(.61, result.Confidence);
        Assert.Equal(0, result.Usage!.OutputTokens);
        Assert.Null(result.Usage.TotalTokens);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task HttpFailureStaysFailure(int status)
    {
        using var handler = new FixtureHandler(
            (HttpStatusCode)status,
            """{"error":{"code":"SyntheticFailure"}}""");
        using var http = new HttpClient(handler);
        using var client = Create(http, new FixtureCredential());

        var error = await Assert.ThrowsAsync<DecisionException>(() =>
            client.ChooseAsync("synthetic", "Which team?", SupportRoutingAgent.Routes));

        Assert.Equal(status, error.StatusCode);
    }

    [Fact]
    public async Task UnauthorizedDiagnosticsAreAttachedToDecisionActivityAndFailureIsPreserved()
    {
        const string principal = "22222222-2222-4222-8222-222222222222";
        const string providerRequestId = "11111111-1111-4111-8111-111111111111";
        const string ticket = "synthetic-private-ticket";
        const string privateClaim = "synthetic-private-claim";
        string token = CreateToken(
            $$"""{"aud":"https://cognitiveservices.azure.com","exp":123,"oid":"{{principal}}","custom":"{{privateClaim}}"}""");
        var (activity, error) = await InvokeUnauthorizedAgentAsync(
            token,
            """{"error":{"code":"PermissionDenied","message":"Principal does not have access to API/Operation"}}""",
            ticket,
            new Dictionary<string, string>
            {
                ["x-ms-request-id"] = providerRequestId,
                ["WWW-Authenticate"] = "Bearer realm=\"synthetic-private-realm\"",
                ["x-private-diagnostic"] = "synthetic-private-header"
            });

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        var tags = activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(principal)));

        Assert.Equal("401", tags["http.response.status_code"]);
        Assert.Equal("True", tags["native_decision.authentication_challenge_present"]);
        Assert.Equal(providerRequestId, tags["native_decision.x_ms_request_id"]);
        Assert.Equal("PermissionDenied", tags["native_decision.provider_error_code"]);
        Assert.Equal(
            "Principal does not have access to API/Operation",
            tags["native_decision.provider_error_message"]);
        Assert.Equal("https://cognitiveservices.azure.com", tags["native_decision.token_audience"]);
        Assert.Equal("123", tags["native_decision.token_expires_unix_seconds"]);
        Assert.Equal(fingerprint, tags["native_decision.token_oid_sha256"]);

        string exportedValues = string.Join(
            "\n",
            activity.TagObjects.Select(tag => tag.Value?.ToString())
                .Concat(tags.Values));
        Assert.DoesNotContain(token, exportedValues);
        Assert.DoesNotContain(principal, exportedValues);
        Assert.DoesNotContain(privateClaim, exportedValues);
        Assert.DoesNotContain(ticket, exportedValues);
        Assert.DoesNotContain("synthetic-private-realm", exportedValues);
        Assert.DoesNotContain("synthetic-private-header", exportedValues);
        Assert.DoesNotContain("synthetic.invalid", exportedValues);
    }

    [Theory]
    [InlineData("[]", "native_decision.provider_error_shape_unexpected")]
    [InlineData("""{"error":"synthetic-private-error"}""", "native_decision.provider_error_shape_unexpected")]
    [InlineData("""{"error":{"code":42,"message":{"private":"synthetic-private-message"}}}""", "native_decision.provider_error_shape_unexpected")]
    [InlineData("""{"error":{"message":"synthetic-private-invalid-json" """, "native_decision.provider_error_body_not_json")]
    public async Task MalformedProviderDiagnosticsStayAllowlistedOnDecisionActivity(
        string body,
        string expectedDiagnostic)
    {
        const string privateAudience = "https://synthetic.invalid/private";
        const string malformedPrincipal = "not-a-guid-do-not-log";
        const string privateClaim = "synthetic-private-claim";
        string token = CreateToken(
            $$"""{"aud":"{{privateAudience}}","exp":9999999999999999999,"oid":"{{malformedPrincipal}}","custom":"{{privateClaim}}"}""");
        var (activity, error) = await InvokeUnauthorizedAgentAsync(token, body, "synthetic-private-ticket");

        Assert.Equal(401, error.StatusCode);
        var tags = activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());
        Assert.Equal("True", tags[expectedDiagnostic]);
        Assert.Equal("unrecognized", tags["native_decision.token_audience"]);
        Assert.Equal("unavailable", tags["native_decision.token_oid_sha256"]);
        Assert.False(tags.ContainsKey("native_decision.token_expires_unix_seconds"));

        string exportedValues = string.Join(
            "\n",
            activity.TagObjects.Select(tag => tag.Value?.ToString())
                .Concat(tags.Values));
        Assert.DoesNotContain(privateAudience, exportedValues);
        Assert.DoesNotContain(malformedPrincipal, exportedValues);
        Assert.DoesNotContain(privateClaim, exportedValues);
        Assert.DoesNotContain("synthetic-private-message", exportedValues);
        Assert.DoesNotContain("synthetic-private-ticket", exportedValues);
        Assert.DoesNotContain(body, exportedValues);
        Assert.DoesNotContain(token, exportedValues);
    }

    [Fact]
    public async Task UnauthorizedDiagnosticsReachDecisionThroughIntermediateHttpActivity()
    {
        var spans = new ConcurrentBag<Activity>();
        using var parent = new Activity("native-client-test").Start();
        using var listener = CreateDecisionActivityListener(spans, parent.Context);
        using var handler = new FixtureHandler(
            HttpStatusCode.Unauthorized,
            """{"error":{"code":"PermissionDenied"}}""");
        using var logs = LoggerFactory.Create(_ => { });
        using var intermediate = new IntermediateActivityHandler
        {
            InnerHandler = new DecisionTransport(logs.CreateLogger("NativeDecision"))
            {
                InnerHandler = handler
            }
        };
        using var http = new HttpClient(intermediate);
        using var client = Create(http, new FixtureCredential());
        var agent = new SupportRoutingAgent(client);

        var error = await Assert.ThrowsAsync<DecisionException>(() => agent.RunAsync("synthetic ticket"));

        Assert.Equal(401, error.StatusCode);
        var decision = Assert.Single(spans);
        Assert.NotNull(intermediate.Activity);
        Assert.Same(decision, intermediate.Activity.Parent);
        Assert.Equal(401, decision.GetTagItem("http.response.status_code"));
        Assert.Equal("PermissionDenied", decision.GetTagItem("native_decision.provider_error_code"));
        Assert.Empty(intermediate.Activity.TagObjects);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedBodyReadFailurePreservesExceptionAndAvailableDiagnostics(bool canceled)
    {
        const string principal = "22222222-2222-4222-8222-222222222222";
        const string providerRequestId = "11111111-1111-4111-8111-111111111111";
        const string privateText = "synthetic-private-body-read-failure";
        string token = CreateToken(
            $$"""{"aud":"https://cognitiveservices.azure.com","exp":123,"oid":"{{principal}}","custom":"{{privateText}}"}""");
        using var cancellation = new CancellationTokenSource();
        Exception original = canceled
            ? new OperationCanceledException(privateText, cancellation.Token)
            : new IOException(privateText);
        var spans = new ConcurrentBag<Activity>();
        using var parent = new Activity("native-client-test").Start();
        using var listener = CreateDecisionActivityListener(spans, parent.Context);
        using var content = new UnreadableContent(original, canceled ? cancellation.Cancel : null);
        using var handler = new UnreadableBodyHandler(content, providerRequestId);
        using var logs = LoggerFactory.Create(_ => { });
        using var http = new HttpClient(new DecisionTransport(logs.CreateLogger("NativeDecision"))
        {
            InnerHandler = handler
        });
        using var client = Create(http, new FixtureCredential(token));
        var agent = new SupportRoutingAgent(client);

        var error = await Record.ExceptionAsync(() =>
            agent.RunAsync("synthetic-private-ticket", cancellationToken: cancellation.Token));

        Assert.NotNull(error);
        if (canceled)
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        var causes = new List<Exception>();
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
            causes.Add(cause);
        Assert.Contains(causes, cause => ReferenceEquals(original, cause));
        Assert.Equal(1, content.ReadAttempts);
        var decision = Assert.Single(spans);
        Assert.Equal(ActivityStatusCode.Error, decision.Status);
        Assert.Equal(401, decision.GetTagItem("http.response.status_code"));
        Assert.Equal(true, decision.GetTagItem("native_decision.authentication_challenge_present"));
        Assert.Equal(providerRequestId, decision.GetTagItem("native_decision.x_ms_request_id"));
        Assert.Equal(true, decision.GetTagItem("native_decision.bearer_metadata_available"));
        Assert.Equal("https://cognitiveservices.azure.com", decision.GetTagItem("native_decision.token_audience"));
        Assert.Equal(123L, decision.GetTagItem("native_decision.token_expires_unix_seconds"));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(principal))),
            decision.GetTagItem("native_decision.token_oid_sha256"));
        Assert.Null(decision.GetTagItem("native_decision.provider_error_code"));
        Assert.Null(decision.GetTagItem("native_decision.failure_diagnostics_wired"));
        string exportedValues = string.Join("\n", decision.TagObjects.Select(tag => tag.Value));
        foreach (string forbidden in new[]
        {
            token, principal, privateText, "synthetic-private-ticket", "synthetic-private-realm"
        })
            Assert.DoesNotContain(forbidden, exportedValues);
    }

    [Fact]
    public async Task UnauthorizedFailureIsPreservedWithoutDecisionActivity()
    {
        using var handler = new FixtureHandler(
            HttpStatusCode.Unauthorized,
            """{"error":{"code":"PermissionDenied"}}""");
        using var logs = LoggerFactory.Create(_ => { });
        using var http = new HttpClient(new DecisionTransport(logs.CreateLogger("NativeDecision"))
        {
            InnerHandler = handler
        });
        using var client = Create(http, new FixtureCredential());

        var error = await Assert.ThrowsAsync<DecisionException>(() =>
            client.ChooseAsync("synthetic", "Which team?", SupportRoutingAgent.Routes));

        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task MalformedBearerPayloadIsNotAddedToDecisionActivity()
    {
        const string token = "synthetic-header.not-valid-base64.synthetic-signature";
        var (activity, error) = await InvokeUnauthorizedAgentAsync(
            token,
            """{"error":{"code":"PermissionDenied"}}""",
            "synthetic-private-ticket");

        Assert.Equal(401, error.StatusCode);
        var tags = activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());
        Assert.Equal("False", tags["native_decision.bearer_metadata_available"]);
        Assert.DoesNotContain(token, string.Join("\n", tags.Values));
    }

    [Fact]
    public async Task SuccessfulNativeCallAddsOnlyNonSensitiveWiringMarker()
    {
        var spans = new ConcurrentBag<Activity>();
        using var parent = new Activity("native-client-test").Start();
        using var listener = CreateDecisionActivityListener(spans, parent.Context);
        var credential = new FixtureCredential();
        const string successBody = """{"answers":{"decision":{"type":"choice","choice":"billing","confidence":0.61,"probabilities":{"billing":0.7,"technical-support":0.1,"account-access":0.1,"manual-review":0.1}}},"usage":{"input_tokens":12,"output_tokens":0}}""";
        using var handler = new FixtureHandler(HttpStatusCode.OK, successBody);
        using var logs = LoggerFactory.Create(_ => { });
        using var http = new HttpClient(new DecisionTransport(logs.CreateLogger("NativeDecision"))
        {
            InnerHandler = handler
        });
        using var client = Create(http, credential);
        var agent = new SupportRoutingAgent(client);

        var result = await agent.RunAsync("synthetic ticket");

        using var decision = JsonDocument.Parse(result.Messages.Single().Text!);
        Assert.Equal("billing", decision.RootElement.GetProperty("team").GetString());
        var activity = Assert.Single(spans);
        Assert.True(Convert.ToBoolean(activity.GetTagItem("native_decision.failure_diagnostics_wired")));
        Assert.DoesNotContain(activity.TagObjects, tag =>
            tag.Key.StartsWith("native_decision.token_", StringComparison.Ordinal));
        Assert.DoesNotContain("synthetic-not-a-real-token", string.Join(
            "\n",
            activity.TagObjects.Select(tag => tag.Value?.ToString())
                .Concat(activity.Events.SelectMany(activityEvent =>
                    activityEvent.Tags.Select(tag => tag.Value?.ToString())))));
    }

    [Fact]
    public async Task MalformedResultStaysFailure()
    {
        using var handler = new FixtureHandler(HttpStatusCode.OK, """{"answers":{}}""");
        using var http = new HttpClient(handler);
        using var client = Create(http, new FixtureCredential());

        await Assert.ThrowsAsync<DecisionException>(() =>
            client.ChooseAsync("synthetic", "Which team?", SupportRoutingAgent.Routes));
    }

    private static FoundryDecisionClient Create(HttpClient http, TokenCredential credential) =>
        new(http, new FoundryDecisionOptions
        {
            Endpoint = new Uri("https://example.services.ai.azure.com/"),
            Model = "test-decision",
            Credential = credential
        });

    private static async Task<(Activity Activity, DecisionException Error)> InvokeUnauthorizedAgentAsync(
        string token,
        string body,
        string ticket,
        Dictionary<string, string>? responseHeaders = null)
    {
        var spans = new ConcurrentBag<Activity>();
        using var parent = new Activity("native-client-test").Start();
        using var listener = CreateDecisionActivityListener(spans, parent.Context);
        using var handler = new FixtureHandler(HttpStatusCode.Unauthorized, body, responseHeaders);
        using var logs = LoggerFactory.Create(_ => { });
        using var http = new HttpClient(new DecisionTransport(logs.CreateLogger("NativeDecision"))
        {
            InnerHandler = handler
        });
        using var client = Create(http, new FixtureCredential(token));
        var agent = new SupportRoutingAgent(client);

        var error = await Assert.ThrowsAsync<DecisionException>(() => agent.RunAsync(ticket));

        return (Assert.Single(spans), error);
    }

    private static ActivityListener CreateDecisionActivityListener(
        ConcurrentBag<Activity> spans,
        ActivityContext parentContext)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SupportRoutingAgent.TelemetrySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                options.Parent.TraceId == parentContext.TraceId
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parentContext.TraceId
                    && activity.ParentSpanId == parentContext.SpanId)
                    spans.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static string CreateToken(string claims)
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(claims))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"synthetic-header.{payload}.synthetic-signature";
    }

    private sealed class FixtureCredential(string token = "synthetic-not-a-real-token") : TokenCredential
    {
        public string? Scope { get; private set; }

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            Scope = Assert.Single(requestContext.Scopes);
            return new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    private sealed class IntermediateActivityHandler : DelegatingHandler
    {
        public Activity? Activity { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var activity = new Activity("synthetic-http").Start();
            Activity = activity;
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class UnreadableContent(Exception error, Action? beforeFailure = null) : HttpContent
    {
        public int ReadAttempts { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempts++;
            beforeFailure?.Invoke();
            return Task.FromException(error);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 128;
            return true;
        }
    }

    private sealed class UnreadableBodyHandler(HttpContent content, string providerRequestId) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = content };
            response.Headers.TryAddWithoutValidation("x-ms-request-id", providerRequestId);
            response.Headers.WwwAuthenticate.ParseAdd("Bearer realm=\"synthetic-private-realm\"");
            return Task.FromResult(response);
        }
    }

    private sealed class FixtureHandler(
        HttpStatusCode status,
        string content,
        Dictionary<string, string>? responseHeaders = null) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public bool HasApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.AbsoluteUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            HasApiKey = request.Headers.Contains("api-key");
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
            if (responseHeaders is not null)
            {
                foreach (var header in responseHeaders)
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
        }
    }
}
