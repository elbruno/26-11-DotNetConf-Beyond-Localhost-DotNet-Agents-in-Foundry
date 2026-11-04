# Beyond Localhost: five demos

The sequence separates **where agent code runs** from **where its model runs**.
There is no Ollama dependency. All model calls target existing authorized Foundry deployments.

| Demo | Agent runtime | Model and identity |
|---|---|---|
| 01 `01-console-agent` | Simple MAF console | Foundry chat model, AzureCliCredential |
| 02 `02-host-ready-agent` | Same behavior, local Responses host, not deployed | Same chat model, DefaultAzureCredential |
| 03 `03-foundry-hosted-agent` | Responses host plus two read-only function tools | Same chat model, DefaultAzureCredential |
| 04 `04-local-decision-agent` | Local MAF console, no HTTP host | Foundry Microsoft-Decision-1, AzureCliCredential |
| 05 `05-foundry-hosted-decision-agent` | Same decision logic in Foundry Responses host | Same native decision deployment, agent-instance managed identity |

Stage 03 retains 02's hosting registrations, project, lock and manifest, but now
adds two in-process tools and their tracing source. Their Program.cs files are
intentionally different; Demo 02 stays unchanged. Demo 04 links Demo 05's routing
logic and contracts. Keep the whole bundle together. Model deployment and agent
hosting are different operations.

## Configure once, then run

From the bundle root, in PowerShell 7:

```powershell
& .\demos\Set-DemoEnvironment.ps1
```

This is the only setup script. Four ordinary visible prompts show existing
process values, otherwise saved values. Blank accepts the displayed value.
Only the absent chat deployment defaults to `gpt-6-luna`, not proof it exists.
No actual resource endpoint or decision deployment is guessed.

| Setting | Meaning |
|---|---|
| FOUNDRY_PROJECT_ENDPOINT | HTTPS chat project URL, `/api/projects/<project>` |
| AZURE_AI_MODEL_DEPLOYMENT_NAME | Existing chat model deployment name |
| Decision__Endpoint | HTTPS native decision resource root, not a project URL |
| Decision__Deployment | Existing Microsoft-Decision-1 deployment name |

All four inputs are validated before any file or process setting changes. The
script atomically replaces `demos\shared\demo-settings.local.json`, prints the
selected settings and also configures the current process. The JSON is ignored
by Git and contains only these four non-secret settings. No keys, passwords,
tokens or credential variables are read, requested or stored. No User/Machine
environment, terminal profile, authentication, build, inference or deployment is
changed. Standard build/test/deploy commands remain separate.

Each project links `shared\DemoSettings.cs` and copies the JSON beside its local
output automatically. New terminals and `dotnet run`/`azd ai agent run` reuse it
without running setup again. A meaningful process variable overrides the saved
setting. Empty/whitespace overrides cannot erase it; whitespace is reported on
stderr. An invalid meaningful override fails explicitly instead of falling back.
Missing or malformed saved settings also fail explicitly. Run setup again only
to change the selected endpoints/deployments. It does not configure azd's
deployment environment.

## Demo 01

```powershell
dotnet run --project .\demos\01-console-agent\ConsoleAgent.csproj
```

AzureCliCredential uses an already authorized CLI identity, with no credential
fallback. Setup does not sign in.

## Demo 02

From `demos\02-host-ready-agent`, run only:

```powershell
azd ai agent run
```

Show the browser's Agent Inspector and enter a synthetic .NET question there.
The installed CLI starts the Responses host and opens its client automatically.
No second terminal or separate local invoke command is part of this demo.
Stop with Ctrl+C afterward.

The hosting registration uses
`builder.WebApplicationBuilder.AddFoundryResponses(agent)`. In the pinned 1.27
hosting package, the IServiceCollection overload is local-only and fails in
Foundry because response/task storage must bind through the same host
configuration. The host-builder overload supports both local and hosted runs.
Saved settings also prevent the former blank-model startup exception.

DefaultAzureCredential in 02/03 can select existing environment credentials
before CLI identity, and existing AZURE_TOKEN_CREDENTIALS may filter the chain.
See [credential chains](https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains).
The helper does not inspect/change credential settings or guarantee an identity.

The installed AgentServer uses ListenAnyIP on port 8088. Use a protected network,
existing inbound firewall protection and no tunnels. ASPNETCORE_URLS alone does
not override this listener. Browser/capture rehearsal is a separate check;
readiness alone does not establish inference or production capabilities.

## Demo 03

Show the deployed agent in the selected project's Foundry Agents page,
Playground and Versions, then compare its source with Demo 02. V08 adds two
deterministic read-only functions in `DemoTools.cs`: `GetDotNetFact()` returns a
fixed .NET garbage-collector fact, and `AddNumbers(a, b)` returns a checked integer
sum. They need no external service or additional credentials. Basic greetings and
the Responses host remain available. No tool changes have been deployed by this
revision. An older hosted version cannot demonstrate the new tools.

After a separately authorized redeployment, use this exact Playground prompt:

```text
Call GetDotNetFact to get the demo's .NET fact, then call AddNumbers with a=19 and b=23. Use both tool results in a brief answer. Do not calculate the sum yourself.
```

Expected tool results: `The .NET garbage collector automatically reclaims memory
occupied by unreachable objects.` and `42`. Final model wording can vary. For a
single-tool demonstration, use either prompt:

```text
Call GetDotNetFact and tell me the demo's .NET fact.
```

```text
Call AddNumbers with a=19 and b=23 and report the tool result.
```

Open **Agents > Traces** in the hosting project and find that invocation by its
Trace ID or Response ID. Expand the tool-orchestration subtree and look for
`execute_tool GetDotNetFact` and `execute_tool AddNumbers`, with
`gen_ai.operation.name=execute_tool` and the matching `gen_ai.tool.name`. Tool
spans prove execution; a fluent answer containing 42 alone does not.

The pinned .NET client is instrumented with `UseOpenTelemetry` before MAF builds
its function-invocation loop. `ConfigureTracing(...AddSource(...))` subscribes the
AgentHost collector to the same `BeyondLocalhost.HelloAgent` ActivitySource.
These are real SDK spans, not handwritten log messages. Raw prompt, argument and
result capture remains disabled. Foundry trace visibility still requires a
configured telemetry destination, linked Application Insights and existing reader
access. No telemetry resource, content-capture setting or permissions are changed.
Local tests/inference are not proof that a future hosted version exports traces.
See [hosted tracing](https://learn.microsoft.com/azure/foundry/observability/quickstarts/quickstart-tracing-hosted-agent)
and [.NET agent observability](https://learn.microsoft.com/agent-framework/agents/observability).

An active version is registration evidence, not proof of successful model access
or tool execution. Use an actual completed response and its spans as evidence.

For a future separately authorized deployment, configure the selected azd
environment explicitly. The setup JSON is **not** azd's `.azure/<env>/.env`.
Use `azd env set` for AZURE_AI_PROJECT_ENDPOINT, AZURE_AI_PROJECT_ID,
AZURE_TENANT_ID, FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME.
The project values must identify the same intended hosting project.

Both chat manifests map their two runtime settings through `env`. Code deployment
uses `dependencyResolution: bundled`, so `azd` publishes .NET and packages the
complete assemblies, including the linked settings helper. Local settings JSON
is explicitly excluded from publish. No Docker/ACR or new model is needed.
After reviewing the environment, `azd deploy hello-agent --no-prompt` creates a
new immutable version. Do not provision a new project or alter access implicitly.

## Demo 04

```powershell
dotnet run --project .\demos\04-local-decision-agent\LocalDecisionAgent.csproj
```

The console prints `Question: I was charged twice for my subscription.` **before**
inference, then parses the returned JSON and prints it indented. Errors remain
errors, not synthetic success. No HTTP host or repeated setup prelude is needed.

The shared MAF agent calls the existing native Microsoft-Decision-1 deployment.
It accepts one nonempty text input, maximum 64 KiB UTF-8. It rejects history,
system/nontext messages and provider failures. The result is a structured route
plus deterministic recommendation text; a person confirms it. No ticketing
system is updated.

An agent can use deterministic rules or traditional ML without an LLM. This
example uses a decision call without a chat/generative explanation call.
Microsoft says Decision-1 is built on Qwen3.5-9B, so do not claim zero
language-model-derived components. Confidence differs from class probability;
neither guarantees correctness. Missing usage remains null.

## Demo 05

The MIT [upstream sample](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/tree/00b60a8a95ad57d5ef975bb914a87f1cec76e049/samples/CoreSamples/DecisionAgent-01-FoundryHosted)
retains its dependency pins, routing logic, contracts, license and notices.
Session adaptations are documented at the top of its README: shared local
settings loading, explicit `env` aliases supported by the installed CLI, and
bundled .NET publishing. The remote-build source ZIP omitted sibling contracts
and linked source; publishing first includes them without copying/rewriting
their implementations.

Deployment configuration remains separate:

| Selected setting | Demo 05 azd input | Runtime variable |
|---|---|---|
| Chat project endpoint, only if hosting in the same project | AZURE_AI_PROJECT_ENDPOINT | Hosting target, not a decision URL |
| Decision__Endpoint | DECISION_ENDPOINT | Decision__Endpoint |
| Decision__Deployment | DECISION_DEPLOYMENT | Decision__Deployment |

Also set the verified AZURE_AI_PROJECT_ID and AZURE_TENANT_ID. Explicitly save
these aliases with `azd env set` in the selected environment; do not assume the
setup file or process values populate azd. `azd deploy support-routing
--no-prompt` publishes a version but never proves native-model permission.

Two identity hops matter: caller to hosted endpoint, then current agent-instance
identity to the native decision resource. AzureCliCredential is used locally;
ManagedIdentityCredential(SystemAssigned) uses the hosted token service in
Foundry, **not the project managed identity**. A token alone grants no downstream
permission. See the [hosted-agent token service](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-token-service).
The setup script does not add roles or store keys.

The optional C# caller needs AGENT_OPENAI_BASE_URL and AGENT_NAME only after an
actual deployment. Obtain them from verified agent details, never guess them or
substitute the model/project endpoint.

## Verification boundary

All five projects build with the existing locked dependency graph. Synthetic
configuration, agent, transport and host tests do not establish cloud readiness,
model accuracy or permissions. Current deployment evidence is tracked in the
private speaker guide, not embedded as tenant-specific configuration here.
Never present a failed native-model response as a completed decision.

References: [primary hosting article](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/),
[hosting APIs](https://learn.microsoft.com/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp),
[Decision-1](https://learn.microsoft.com/azure/foundry/foundry-models/how-to/use-foundry-models-microsoft-decision).
