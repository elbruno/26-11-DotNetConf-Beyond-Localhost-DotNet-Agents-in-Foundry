// Purpose: Run the hosted sample's decision agent directly, without an HTTP host.
// Requires an existing Foundry Decision__Endpoint and Decision__Deployment, plus
// separately authorized Azure CLI sign-in. Expected result: one typed routing result.
using Azure.Identity;
using ElBruno.AI.Decisions.Foundry;
using SupportRouting;

// Step 1: Configure the native decision deployment, not a chat project endpoint.
var endpoint = new Uri(Environment.GetEnvironmentVariable("Decision__Endpoint")
    ?? throw new InvalidOperationException("Set Decision__Endpoint to the native Foundry resource root."));
var deployment = Environment.GetEnvironmentVariable("Decision__Deployment")
    ?? throw new InvalidOperationException("Set Decision__Deployment to the existing decision deployment.");

using var decisions = new FoundryDecisionClient(new FoundryDecisionOptions
{
    Endpoint = endpoint,
    Model = deployment,
    Credential = new AzureCliCredential(),
    Timeout = TimeSpan.FromSeconds(45)
});

// Step 2: Run the exact same agent logic that Demo 05 exposes through Responses.
const string ticket = "I was charged twice for my subscription.";
var response = await new SupportRoutingAgent(decisions).RunAsync(ticket);

// Step 3: Print the bounded decision, not a generated chat explanation.
Console.WriteLine(response.Messages.Single().Text);
