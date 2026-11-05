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

    private static string CreateToken(string claims)
    {
        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(claims))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"synthetic-header.{payload}.synthetic-signature";
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"code":"PermissionDenied","message":"Principal does not have access to API/Operation"}}""")
            };
            response.Headers.TryAddWithoutValidation("x-ms-request-id", ProviderRequestId);
            response.Headers.TryAddWithoutValidation("x-private-diagnostic", "private-header-do-not-log");
            return Task.FromResult(response);
        }
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
