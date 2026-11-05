// Purpose: Verify useful authentication diagnostics never expose credentials or ticket text.
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SupportRouting;
using Xunit;

namespace SupportRoutingTests;

public sealed class DecisionTransportTests
{
    private const string PrincipalId = "22222222-2222-4222-8222-222222222222";
    private const string ProviderRequestId = "11111111-1111-4111-8111-111111111111";

    [Fact]
    public async Task UnauthorizedResponseKeepsFailureAndLogsOnlyAllowlistedDiagnostics()
    {
        var logger = new RecordingLogger();
        using var transport = new DecisionTransport(logger) { InnerHandler = new UnauthorizedHandler() };
        using var http = new HttpClient(transport);
        string token = CreateToken(
            $$"""{"aud":"https://cognitiveservices.azure.com","exp":123,"oid":"{{PrincipalId}}","private":"never-log-this"}""");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("synthetic-ticket-do-not-log");

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string output = string.Join("\n", logger.Messages);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(PrincipalId)));
        Assert.Contains("401", output);
        Assert.Contains("PermissionDenied", output);
        Assert.Contains("Principal does not have access to API/Operation", output);
        Assert.Contains("https://cognitiveservices.azure.com", output);
        Assert.Contains("expires=123", output);
        Assert.Contains($"oidSha256={fingerprint}", output);
        Assert.Contains($"x-ms-request-id={ProviderRequestId}", output);
        Assert.DoesNotContain(token, output);
        Assert.DoesNotContain(PrincipalId, output);
        Assert.DoesNotContain("never-log-this", output);
        Assert.DoesNotContain("synthetic-ticket-do-not-log", output);
        Assert.DoesNotContain("private-header-do-not-log", output);
    }

    [Fact]
    public async Task UnrecognizedAudienceAndMalformedPrincipalAreNotEchoed()
    {
        var logger = new RecordingLogger();
        using var transport = new DecisionTransport(logger) { InnerHandler = new UnauthorizedHandler() };
        using var http = new HttpClient(transport);
        const string privateAudience = "https://synthetic.invalid/private";
        const string malformedPrincipal = "not-a-guid-do-not-log";
        string token = CreateToken(
            $$"""{"aud":"{{privateAudience}}","exp":-1,"oid":"{{malformedPrincipal}}","private":"claim-do-not-log"}""");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string output = string.Join("\n", logger.Messages);
        Assert.Contains("audience=unrecognized", output);
        Assert.Contains("oidSha256=unavailable", output);
        Assert.DoesNotContain(privateAudience, output);
        Assert.DoesNotContain(malformedPrincipal, output);
        Assert.DoesNotContain("claim-do-not-log", output);
        Assert.DoesNotContain(token, output);
    }

    [Fact]
    public async Task MalformedBearerPayloadDoesNotChangeUnauthorizedFailure()
    {
        var logger = new RecordingLogger();
        using var transport = new DecisionTransport(logger) { InnerHandler = new UnauthorizedHandler() };
        using var http = new HttpClient(transport);
        const string token = "synthetic-header.not-valid-base64.synthetic-signature";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string output = string.Join("\n", logger.Messages);
        Assert.Contains("metadata unavailable", output);
        Assert.DoesNotContain(token, output);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"synthetic-private-root\"")]
    [InlineData("null")]
    public async Task NonObjectErrorRootsPreserveUnauthorizedAndLogSafeShapeDiagnostic(string body)
    {
        string output = await SendUnauthorizedAsync(body);

        Assert.Contains("unexpected JSON shape", output);
        Assert.DoesNotContain("synthetic-private-root", output);
        Assert.DoesNotContain(body, output);
    }

    [Fact]
    public async Task NonObjectErrorPreservesUnauthorizedAndLogsSafeShapeDiagnostic()
    {
        const string body = """{"error":"synthetic-private-error"}""";

        string output = await SendUnauthorizedAsync(body);

        Assert.Contains("unexpected JSON shape", output);
        Assert.DoesNotContain("synthetic-private-error", output);
        Assert.DoesNotContain(body, output);
    }

    [Fact]
    public async Task NonStringCodeAndMessagePreserveUnauthorizedAndLogSafeShapeDiagnostic()
    {
        const string body = """{"error":{"code":42,"message":{"private":"synthetic-private-message"}}}""";

        string output = await SendUnauthorizedAsync(body);

        Assert.Contains("unexpected JSON shape", output);
        Assert.DoesNotContain("synthetic-private-message", output);
        Assert.DoesNotContain(body, output);
    }

    [Fact]
    public async Task InvalidErrorJsonPreservesUnauthorizedAndLogsOnlyGenericDiagnostic()
    {
        const string body = """{"error":"synthetic-private-invalid-json" """;

        string output = await SendUnauthorizedAsync(body);

        Assert.Contains("Native authentication error body was not JSON", output);
        Assert.DoesNotContain("synthetic-private-invalid-json", output);
        Assert.DoesNotContain(body, output);
    }

    [Fact]
    public async Task TransportFailureRemainsObservable()
    {
        using var transport = new DecisionTransport(new RecordingLogger())
        {
            InnerHandler = new FailingHandler(new HttpRequestException("synthetic transport failure"))
        };
        using var http = new HttpClient(transport);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => http.SendAsync(request));

        Assert.Equal("synthetic transport failure", exception.Message);
    }

    [Fact]
    public async Task CancellationRemainsObservable()
    {
        using var transport = new DecisionTransport(new RecordingLogger())
        {
            InnerHandler = new FailingHandler(new OperationCanceledException("synthetic cancellation"))
        };
        using var http = new HttpClient(transport);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => http.SendAsync(request));
    }

    private static async Task<string> SendUnauthorizedAsync(string body)
    {
        var logger = new RecordingLogger();
        using var transport = new DecisionTransport(logger) { InnerHandler = new UnauthorizedHandler(body) };
        using var http = new HttpClient(transport);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        return string.Join("\n", logger.Messages);
    }

    private static string CreateToken(string claims)
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(claims))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"synthetic-header.{payload}.synthetic-signature";
    }

    private sealed class UnauthorizedHandler(
        string body = """{"error":{"code":"PermissionDenied","message":"Principal does not have access to API/Operation"}}""")
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(body)
            };
            response.Headers.TryAddWithoutValidation("x-ms-request-id", ProviderRequestId);
            response.Headers.TryAddWithoutValidation("x-private-diagnostic", "private-header-do-not-log");
            return Task.FromResult(response);
        }
    }

    private sealed class FailingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
