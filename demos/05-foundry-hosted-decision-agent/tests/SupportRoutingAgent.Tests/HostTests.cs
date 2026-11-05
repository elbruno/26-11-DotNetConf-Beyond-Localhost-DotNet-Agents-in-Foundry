/*
 * Purpose: Exercise the official Responses adapter on a loopback test host.
 * Synthetic decision fixtures validate the HTTP/SSE contract without cloud inference.
 */
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.AgentServer.Core;
using ElBruno.AI.Decisions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SupportRouting;
using Xunit;

namespace SupportRoutingTests;

public sealed class HostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialAdapterReturnsDecision(bool stream)
    {
        var fixture = new FixtureClient();
        await using var app = await Start(fixture);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await http.PostAsJsonAsync(
            "/responses",
            new { input = "synthetic ticket", stream, store = false });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        if (stream)
        {
            Assert.Contains("response.completed", body);
            Assert.DoesNotContain("response.failed", body);
            Assert.Contains("billing", body);
        }
        else
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
            var output = json.RootElement.GetProperty("output")[0].GetProperty("content")[0];
            Assert.Equal("output_text", output.GetProperty("type").GetString());
            using var decision = JsonDocument.Parse(output.GetProperty("text").GetString()!);
            Assert.Equal("billing", decision.RootElement.GetProperty("team").GetString());
        }

        Assert.Equal(1, fixture.Calls);
    }

    [Fact]
    public async Task FailedModelProducesFailedSseNotRoutingFallback()
    {
        await using var app = await Start(new FixtureClient
        {
            Failure = new DecisionException("Synthetic failure")
        });
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var response = await http.PostAsJsonAsync(
            "/responses",
            new { input = "synthetic ticket", stream = true, store = false });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("response.failed", body);
        Assert.DoesNotContain("response.completed", body);
        Assert.DoesNotContain("Suggested team:", body);
    }

    [Fact]
    public async Task AgentHostCollectorReceivesNativeDecisionActivity()
    {
        var spans = new ConcurrentBag<Activity>();
        var fixture = new FixtureClient();
        var agent = new SupportRoutingAgent(fixture);
        var builder = AgentHost.CreateBuilder();
        builder.ConfigureTracing(tracing => tracing.AddSource(SupportRoutingAgent.TelemetrySource)
            .AddProcessor(new CaptureProcessor(spans)));
        builder.Services.AddFoundryResponses(agent);
        builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
        await using var app = builder.Build().App;
        _ = app.Services.GetRequiredService<TracerProvider>();

        await agent.RunAsync("synthetic ticket");

        Assert.Equal(1, fixture.Calls);
        var activity = Assert.Single(spans, span => span.DisplayName == "decision.route_support");
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal("choose", activity.GetTagItem("decision.operation"));
    }

    private sealed class CaptureProcessor(ConcurrentBag<Activity> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => spans.Add(data);
    }

    private static async Task<WebApplication> Start(FixtureClient fixture)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddFoundryResponses(new SupportRoutingAgent(fixture));

        var app = builder.Build();
        app.MapFoundryResponses();
        await app.StartAsync();
        return app;
    }
}
