// Purpose: Verify real function execution and content-free SDK spans in both response modes.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

public sealed class KimiToolAgentTests
{
    [Fact]
    public async Task CancellationIsNotConvertedToSuccess()
    {
        using var model = new ToolCallingClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KimiToolAgent.Create(model).RunAsync("Use the tools.",
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task ProviderFailureIsNotConvertedToSuccess()
    {
        var failure = new InvalidOperationException("Synthetic model failure.");
        using var model = new ToolCallingClient(failure);
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            KimiToolAgent.Create(model).RunAsync("Use the tools."));
        Assert.Same(failure, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostCollectsModelAndAllThreeToolSpans(bool streaming)
    {
        var spans = new ConcurrentBag<Activity>();
        using var model = new ToolCallingClient();
        var agent = KimiToolAgent.Create(model);
        Assert.Equal("netconf-kimi-tools", agent.Name);
        Assert.Equal("Kimi-K3.1-Pro", KimiToolAgent.Model);

        var builder = AgentHost.CreateBuilder();
        builder.ConfigureTracing(tracing => tracing.AddSource(KimiToolAgent.TelemetrySource)
            .AddProcessor(new CaptureProcessor(spans)));
        builder.WebApplicationBuilder.AddFoundryResponses(agent);
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        _ = app.Services.GetRequiredService<TracerProvider>();

        using var parent = new Activity("kimi-tool-test");
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        parent.Start();
        if (streaming)
        {
            await foreach (var _ in agent.RunStreamingAsync("Use the three tools.")) { }
        }
        else
        {
            var response = await agent.RunAsync("Use the three tools.");
            Assert.Equal("42; 6; unreachable objects.", response.Text);
        }

        Assert.True(model.ReceivedResults);
        foreach (var name in new[] { "GetDotNetFact", "AddNumbers", "CountWords" })
        {
            var span = Assert.Single(spans, span =>
                span.GetTagItem("gen_ai.tool.name")?.ToString() == name);
            Assert.Equal("execute_tool", span.GetTagItem("gen_ai.operation.name"));
            Assert.Equal(parent.TraceId, span.TraceId);
            Assert.Null(span.GetTagItem("gen_ai.tool.call.arguments"));
            Assert.Null(span.GetTagItem("gen_ai.tool.call.result"));
        }
        Assert.Contains(spans, span =>
            span.GetTagItem("gen_ai.request.model")?.ToString() == KimiToolAgent.Model);
    }

    private sealed class CaptureProcessor(ConcurrentBag<Activity> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => spans.Add(data);
    }

    private sealed class ToolCallingClient(Exception? failure = null) : IChatClient
    {
        public bool ReceivedResults { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (failure is not null) { return Task.FromException<ChatResponse>(failure); }
            var results = messages.SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>().ToArray();
            ChatMessage output;
            if (results.Length == 0)
            {
                Assert.Equal(["GetDotNetFact", "AddNumbers", "CountWords"],
                    options!.Tools!.Select(tool => tool.Name));
                output = new ChatMessage(ChatRole.Assistant,
                [
                    new FunctionCallContent("fact", "GetDotNetFact"),
                    new FunctionCallContent("sum", "AddNumbers",
                        new Dictionary<string, object?> { ["a"] = 19, ["b"] = 23 }),
                    new FunctionCallContent("words", "CountWords",
                        new Dictionary<string, object?> { ["text"] = "Foundry traces show real tool calls" })
                ]);
            }
            else
            {
                Assert.Contains(results, result => result.CallId == "sum"
                    && JsonSerializer.SerializeToElement(result.Result).GetInt32() == 42);
                Assert.Contains(results, result => result.CallId == "words"
                    && JsonSerializer.SerializeToElement(result.Result).GetInt32() == 6);
                Assert.Contains(results, result => result.CallId == "fact"
                    && result.Result?.ToString()?.Contains("unreachable objects") == true);
                ReceivedResults = true;
                output = new ChatMessage(ChatRole.Assistant, "42; 6; unreachable objects.");
            }
            return Task.FromResult(new ChatResponse(output) { ModelId = KimiToolAgent.Model });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) { yield return update; }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }
    }
}
