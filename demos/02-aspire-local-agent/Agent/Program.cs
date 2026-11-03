// Purpose: Run the same HelloAgent behavior using an existing local Ollama model.
// Requires a loopback OLLAMA_ENDPOINT and an installed OLLAMA_MODEL_NAME chat model.
// Expected result: one real local model response, not a canned answer or Foundry call.
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

// Step 1: Require the existing local service and chat deployment explicitly.
var endpoint = new Uri(Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
    ?? throw new InvalidOperationException("Set OLLAMA_ENDPOINT before running."));
if (!endpoint.IsLoopback || endpoint.Scheme is not ("http" or "https") ||
    endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
{
    throw new InvalidOperationException("OLLAMA_ENDPOINT must be an HTTP(S) loopback URL without credentials, query or fragment.");
}
var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL_NAME");
if (string.IsNullOrWhiteSpace(model))
{
    throw new InvalidOperationException("Set OLLAMA_MODEL_NAME to an already-installed chat-capable model.");
}

// Step 2: Change the provider, keeping the agent's name and instructions.
using var client = new OllamaApiClient(endpoint, model);
AIAgent agent = client.AsAIAgent(
    instructions: "You are a friendly assistant. Keep your answers brief.",
    name: "HelloAgent");

// Step 3: Ask once, with no tool execution or cloud credential.
Console.WriteLine(await agent.RunAsync("Hello! Tell me a fun fact about .NET."));
