# Beyond Localhost: five demos

The sequence separates **where agent code runs** from **where its model runs**. There is no Ollama dependency in this active narrative. All model calls target existing authorized Foundry deployments.

| Demo | Agent runtime | Model and identity | Preparation status |
|---|---|---|---|
| 01 `01-console-agent` | Simple MAF console | Foundry chat model, AzureCliCredential | Built, inference unverified |
| 02 `02-host-ready-agent` | Same behavior, local Responses host, not deployed | Same chat model, DefaultAzureCredential | Built, not deployed |
| 03 `03-foundry-hosted-agent` | Same host source intended for deployment in Foundry | Same chat model, DefaultAzureCredential | Deployment-ready source only, deployment/inference unverified |
| 04 `04-local-decision-agent` | Local MAF console, no HTTP host | Foundry Microsoft-Decision-1, AzureCliCredential | Built, inference unverified |
| 05 `05-foundry-hosted-decision-agent` | Same decision logic in Foundry Responses host | Same decision deployment, managed identity when hosted | Exact public sample, deployment/inference unverified |

Stages 02 and 03 have byte-identical host source, lock and manifest: the intended change is deployment, not agent behavior. Demo 04 compiles the exact `SupportRoutingAgent.cs` and contracts from Demo 05 through linked source/project references, avoiding divergent routing logic. Keep the whole audience bundle together; Demo 04 is not a standalone-folder export. Model deployment and hosted-agent deployment are distinct. Folder names do not prove cloud resources exist.

## One configuration script for all five demos

In the PowerShell 7 terminal used for the demos, from the bundle root:

```powershell
& .\demos\Set-DemoEnvironment.ps1
```

This script configures only four non-secret settings, using visible prompts and showing the selected values:

| Setting | Meaning and blank-input behavior |
|---|---|
| FOUNDRY_PROJECT_ENDPOINT | Existing HTTPS chat project URL with `/api/projects/<project>` path; retain current process value if present, otherwise required |
| AZURE_AI_MODEL_DEPLOYMENT_NAME | Existing chat deployment; retain current value, otherwise default to `gpt-6-luna` |
| Decision__Endpoint | Existing HTTPS native Decision-1 resource root, not a project URL; retain current value if present, otherwise required |
| Decision__Deployment | Existing Microsoft-Decision-1 deployment name; retain current value if present, otherwise required |

Current values/defaults appear in brackets. Blank accepts that value; no real endpoint or decision deployment is guessed. Defaults do not prove deployments exist. All four inputs are validated before any process setting changes. Invalid input produces an explicit error and leaves all four settings unchanged.

No keys, passwords or tokens are requested, stored or printed. No credential variables are read, set or cleared. No files or User/Machine environment settings are written. There is no restore, build, export, login, inference, provisioning, deployment or access change in this script. It does not change location. Use `&`, not `pwsh -File`, which configures a child process that exits. Configure every separate terminal independently. Child processes inherit the four plaintext non-secret settings.

The former build/export helpers are retired from active surfaces. Build an individual project with standard `dotnet build <project> -c Release`; builds and tests remain separate from configuration. Locked dependencies use SDK 10.0.403 with patch roll-forward. Demo 04 uses MAF 1.22.0, Azure.Identity 1.21.0 and Decisions.Foundry 0.6.3 to match Demo 05's logic. Demo 05 retains exact upstream pins rather than adopting the chat host version.

## Chat Demos 01 to 03

Demo 01 uses AzureCliCredential explicitly with no fallback and requires separately approved Azure CLI sign-in and model/project access:

```powershell
dotnet run --project .\demos\01-console-agent\ConsoleAgent.csproj
```

Demo 02 uses DefaultAzureCredential, as does identical deployment-stage Demo 03. Existing environment credentials can precede CLI sign-in, and existing AZURE_TOKEN_CREDENTIALS may filter the chain. The helper does not inspect/change these settings or guarantee an identity. See [credential chains](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/credential-chains).

After separately approved configuration, from `demos\02-host-ready-agent`:

```powershell
azd ai agent run --no-client
```

In another terminal in that folder:

```powershell
azd ai agent invoke --local "Hello! Tell me a fun fact about .NET."
```

This is the host-ready **not deployed** stage. The three registrations plus normal build/run lifecycle expose Responses; the full difference from Demo 01 also includes identity, not literally only three lines.

The installed AgentServer uses ListenAnyIP on port 8088. It may be LAN-reachable. Use a controlled protected network, existing inbound firewall protection and no tunnels; ASPNETCORE_URLS alone does not override this listener. Stop the host when finished. `/readiness` success proves startup, not inference or cloud persistence. Local storage/resilience/telemetry are not proof of configured platform features.

Demo 03 uses the same source after separately authorized deployment. Its runtime manifest declares `dotnet_10` and Responses 2.0.0, required by hosting 1.27.0-preview.261030.1. Initialization/environment selection, quota and approved identity/project/model access precede cloud action. Current CLI help prefers `init --template/-t`. `azd provision` can bill, `azd deploy` publishes code and `azd down` deletes resources. None was run or authorized by this preparation. Do not label Demo 03 deployed until actual evidence exists.

## Decision configuration for Demos 04 and 05

These call **Microsoft-Decision-1 deployed in Foundry**, never a local decision model or Ollama. The same setup script above configures their separate resource-root endpoint and deployment:

```powershell
& .\demos\Set-DemoEnvironment.ps1
```

Endpoint is the native resource root, not the chat project's `/api/projects/...` path. Deployment is the existing decision model, not `gpt-6-luna`. Setting variables does not deploy a model or authenticate.

Demo 04 runs the real custom AIAgent in-process with AzureCliCredential:

```powershell
dotnet run --project .\demos\04-local-decision-agent\LocalDecisionAgent.csproj
```

The fixed duplicate-charge ticket produces a structured route and deterministic recommendation text. A person must confirm. It does not update a ticketing system. Shared source accepts one nonempty text user input, max 64 KiB UTF-8, no history/system/nontext. Provider failures remain failures, not success-shaped manual-review routes.

Demo 05 preserves the exact MIT [blog-linked public sample](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/tree/00b60a8a95ad57d5ef975bb914a87f1cec76e049/samples/CoreSamples/DecisionAgent-01-FoundryHosted), its original source, pinned dependencies, LICENSE and THIRD-PARTY-NOTICES. Follow its README/manifest for the hosted shape; do not execute installation/login/deployment/cleanup without separate approval. It chooses AzureCliCredential locally and ManagedIdentityCredential(SystemAssigned) when hosted, as published. That narrow source-fidelity exception differs from the earlier DefaultAzureCredential preference for deployable projects; Demos 02/03 remain DefaultAzureCredential.

Two identity hops matter: the caller is authorized to invoke the hosted agent; independently, the agent-instance identity needs access to the decision-model resource. The [hosted-agent token service](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agent-token-service) supplies the current agent-instance identity through ManagedIdentityCredential, not the project managed identity. Getting a token does not grant downstream permissions. No API key, automatic RBAC/access grant or Azure CLI identity inside Foundry is assumed.

Demo 05's unchanged manifest uses AZURE_AI_PROJECT_ENDPOINT for the existing hosting project and maps DECISION_ENDPOINT/DECISION_DEPLOYMENT to runtime Decision__Endpoint/Decision__Deployment. The helper sets runtime process values only; it does not populate azd's saved environment or infer aliases. If the same project will host Demo 05, the corresponding mappings are:

| Selected process setting | Demo 05 azd environment input |
|---|---|
| FOUNDRY_PROJECT_ENDPOINT | AZURE_AI_PROJECT_ENDPOINT |
| Decision__Endpoint | DECISION_ENDPOINT |
| Decision__Deployment | DECISION_DEPLOYMENT |

After separately approving deployment preparation and selecting the correct azd environment, explicitly copy those same selected values with `azd env set` as described in the original sample README. If a different project will host the agent, supply that explicitly instead of assuming the chat project. Demo 03's manifest directly references FOUNDRY_PROJECT_ENDPOINT and AZURE_AI_MODEL_DEPLOYMENT_NAME; its saved azd environment must also be explicitly reviewed/configured. This is separate deployment preparation, not something the helper performs or verifies.

The optional Demo 05 C# caller needs AGENT_OPENAI_BASE_URL and AGENT_NAME only after an agent has actually been deployed. Obtain them from verified deployed-agent details; do not use the project/model endpoint or guess an agent endpoint. They are not required by the setup script before deployment.

The inspected episode uses different service names and `env`/AZURE_AI_MODEL_DEPLOYMENT_NAME mappings and persists user environment/UserSecrets in its setup helper. Those are reference patterns, not adopted configuration. Do not mix manifests or replace this process-only helper with the episode's persistent setup.

An agent in general can use deterministic rules or traditional ML without an LLM. This sample makes a structured Decision-1 call without a chat/generative explanation call. Microsoft describes Decision-1 as built on Qwen3.5-9B, so do not claim zero language-model-derived components in this particular example. Provider confidence and class probability are distinct, neither guarantees correctness.

Offline checks from Demo 05:

```powershell
dotnet test .\tests\SupportRoutingAgent.Tests -c Release --no-restore
```

All 26 original tests use fake decisions/HTTP/credentials and loopback Responses hosting. They test the exact logic shared by Demo 04 and the host/caller contracts, not actual cloud inference, model accuracy, permission sufficiency or deployment. Historical success documented by an external sample is not newly verified success for this bundle.

References: [primary hosting article](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/), [Agent Framework hosting](https://learn.microsoft.com/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp), [hosted-agent concepts](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents), [Decision-1 background](https://labs.ai.azure.com/innovations/microsoft-decision-1/).
