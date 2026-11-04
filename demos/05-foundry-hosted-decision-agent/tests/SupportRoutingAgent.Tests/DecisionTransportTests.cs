// Purpose: Keep authentication diagnostics useful without logging credentials or ticket text.
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using SupportRouting;
using Xunit;

namespace SupportRoutingTests;

public sealed class DecisionTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnauthorizedResponseKeepsFailureAndDoesNotLogToken(bool validMetadata)
    {
        var logger = new RecordingLogger();
        using var transport = new DecisionTransport(logger) { InnerHandler = new UnauthorizedHandler() };
        using var http = new HttpClient(transport);
        string payload = validMetadata
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"aud":"https://synthetic.invalid","exp":123,"private":"never-log-this"}"""))
            : "not-valid-base64";
        string token = $"synthetic-header.{payload}.synthetic-signature";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://synthetic.invalid");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("synthetic-ticket-do-not-log");

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string output = string.Join("\n", logger.Messages);
        Assert.Contains("401", output);
        Assert.Contains("PermissionDenied", output);
        Assert.Contains("Principal does not have access to API/Operation", output);
        Assert.DoesNotContain(token, output);
        Assert.DoesNotContain("never-log-this", output);
        Assert.DoesNotContain("synthetic-ticket-do-not-log", output);
        if (validMetadata) Assert.Contains("https://synthetic.invalid", output);
        else Assert.Contains("metadata unavailable", output);
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"code":"PermissionDenied","message":"Principal does not have access to API/Operation"}}""")
            });
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
