#:sdk Aspire.AppHost.Sdk@13.6.4
#:property AspireUseCliBundle=true

// Purpose: Orchestrate HelloAgent against an existing local Ollama service.
// Requires OLLAMA_ENDPOINT and an already-installed OLLAMA_MODEL_NAME chat model.
// No service, image, model or Azure resource is created. Start the agent explicitly.
var builder = DistributedApplication.CreateBuilder(args);
var endpoint = new Uri(Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
    ?? throw new InvalidOperationException("Set OLLAMA_ENDPOINT to the existing local Ollama service."));
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

var ollama = builder.AddExternalService("ollama", endpoint);
builder.AddProject("local-agent", "./Agent/LocalAgent.csproj")
    .WithEnvironment("OLLAMA_ENDPOINT", ollama)
    .WithEnvironment("OLLAMA_MODEL_NAME", model)
    .WithExplicitStart();

builder.Build().Run();
