/*
 * Purpose: Configure the same agent for local development and Foundry hosting.
 * 1. Read the native decision resource root and deployment from external configuration.
 * 2. Use the developer CLI credential locally and the agent's system identity in Foundry.
 * 3. Expose the agent through the official Responses 2.0.0 host.
 */
using Azure.AI.AgentServer.Core;
using Azure.Core;
using Azure.Identity;
using ElBruno.AI.Decisions.Foundry;
using Microsoft.Agents.AI.Foundry.Hosting;
using SupportRouting;

var builder = AgentHost.CreateBuilder(args);
var endpoint = DemoSettings.Read("Decision__Endpoint");
var deployment = DemoSettings.Read("Decision__Deployment");

TokenCredential credential = string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT"))
    ? new AzureCliCredential()
    : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);

using var logs = LoggerFactory.Create(logging => logging.AddConsole());
using var http = new HttpClient(new DecisionTransport(logs.CreateLogger("NativeDecision")));
using var decisions = new FoundryDecisionClient(http, new FoundryDecisionOptions
{
    Endpoint = new Uri(endpoint),
    Model = deployment,
    Credential = credential,
    Timeout = TimeSpan.FromSeconds(45)
});

builder.Services.AddFoundryResponses(new SupportRoutingAgent(decisions));
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();

public partial class Program;
