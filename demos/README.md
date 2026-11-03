# Beyond Localhost demos

Four independent teaching stages. The main narrative follows [From dotnet run to Foundry Hosted Agent in 3 lines of C#](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/).

| Stage | Folder | Provider and identity |
|---|---|---|
| 01 | `01-console-agent` | Foundry model, explicit AzureCliCredential |
| 02 | `02-aspire-local-agent` | Existing local Ollama chat model under Aspire, no Foundry SDK/backend or cloud credential |
| 03 | `03-foundry-hosted-agent` | Foundry Responses host, DefaultAzureCredential |
| 04 | `04-decision-agent` | Exact public decision-routing sample, AzureCliCredential locally and ManagedIdentityCredential in Foundry |

These are source/build-ready stages, not proof of live inference or cloud deployment. Stage 02 still requires an installed chat-capable model. Decision-only Ollama models cannot substitute for one. No model or image is downloaded automatically.

## Preparation without inference

From the bundle root in PowerShell 7:

```powershell
pwsh -File .\Prepare-Demos.ps1
```

This restores locked dependencies and builds all four stages, including the decision caller/tests. It does not start Aspire, authenticate, install software, download models, invoke inference or deploy. Aspire's AppHost is compiled and its graph evaluated separately when you start it. The SDK pin is .NET 10.0.403 with patch roll-forward. Stage 02 uses Microsoft.Agents.AI 1.27.0 and OllamaSharp 5.5.0. Stages 01/03 retain their existing Foundry package pins; stage 04 deliberately retains its upstream versions.

## 01: the standalone Foundry-backed agent

In the terminal you will use for stages 01 and 03, run:

```powershell
& .\demos\Set-DemoEnvironment.ps1
dotnet run --project .\demos\01-console-agent\ConsoleAgent.csproj
```

The helper asks only for the visible project endpoint and model deployment name. Blank model input selects `gpt-6-luna`, or supply a valid existing deployment name. This is a suggested name, not evidence that the deployment exists. There is no default endpoint or implicit fallback in either program. Both inputs are validated before the helper sets only `FOUNDRY_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME`.

The helper does not ask for, read, set, clear or print credentials, authenticate or write inputs to files. Use `&` as shown, not `pwsh -File`, which configures a short-lived child process. Values belong to the current process and its future children; configure another terminal separately. Process environment values are plaintext, not an encrypted credential store. No User/Machine environment or working directory is changed. Enter actual values locally, never in Copilot.

The local-only console uses AzureCliCredential directly, with no silent fallback. It requires your separately approved Azure CLI sign-in and appropriate project/model access. No sign-in is performed by preparation.

## 02: a local agent under Aspire, without Foundry

Requirements: installed Aspire CLI 13.6.4, an already-running local Ollama service and an already-installed **chat-capable** model. For example, `qwen3:0.6b` is a possible small model prerequisite, not a model this bundle installs or has live-verified. Confirm its license, availability and capabilities separately. Do not select a decision-only model for this chat example.

In the PowerShell terminal that starts Aspire, set your existing service URL and installed model:

```powershell
$env:OLLAMA_ENDPOINT = 'http://localhost:11434'
$env:OLLAMA_MODEL_NAME = '<already-installed-chat-model>'
aspire start --apphost .\demos\02-aspire-local-agent\apphost.cs --isolated --non-interactive
```

Aspire declares an external service and passes its reference into `OLLAMA_ENDPOINT`. It does not create another Ollama server, container or model, pull an image, download a model or define Azure infrastructure. The agent accepts only an HTTP(S) loopback endpoint. It uses OllamaSharp's IChatClient with the actual Agent Framework agent, retaining HelloAgent's brief instructions and one-shot .NET-fact prompt.

The `local-agent` resource is intentionally **explicit-start**, so opening the dashboard does not invoke a model. After separately authorizing and preparing live local inference, start that resource from the dashboard. It runs once and exits. Do not start it during build/graph-only checks. `aspire describe --apphost ...` reports actual state; `NotStarted` does not mean model readiness. An external service entry is not a model-capability check.

Stop only this AppHost when finished:

```powershell
aspire stop --apphost .\demos\02-aspire-local-agent\apphost.cs --non-interactive
```

Keep the Ollama API local and controlled; this example does not add authentication to it. Do not tunnel it. No model inference has been verified here. A synthetic loopback HTTP fixture exercises the real provider path, not model quality or a live daemon.

Reference: [official Agent Framework Ollama integration](https://learn.microsoft.com/agent-framework/integrations/by-component/model-providers/ollama).

## 03: the Foundry-host-ready agent

The name, instructions and model story match stage 01. The teaching diff contains the three host registrations plus the deliberate identity difference: the deployable example uses DefaultAzureCredential, including when run locally. Existing environment credentials can precede CLI sign-in and credential selectors may restrict that chain. The helper does not inspect or change those settings. See [credential chains](https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains).

After configuration, from `demos\03-foundry-hosted-agent`:

```powershell
azd ai agent run --no-client
```

In another terminal in the same folder:

```powershell
azd ai agent invoke --local "Hello! Tell me a fun fact about .NET."
```

The installed AgentServer SDK uses `ListenAnyIP` on port 8088. It binds all interfaces and may be LAN-reachable. Use a controlled network with existing inbound firewall protection, no public exposure and no tunnels. `ASPNETCORE_URLS` alone does not override this listener. Review protection before running; no OS/firewall setting is changed here. Stop the host when finished. A readiness response from `/readiness` does not prove inference, persistence or cloud deployment.

The hosting pin `1.27.0-preview.261030.1` requires Responses **2.0.0**. `azure.yaml` describes code runtime `dotnet_10` and the protocol, not an existing deployed environment. Current `azd ai agent init --help` prefers `--template/-t`. Initialization, project/identity selection and reviewed generated configuration precede any authorized cloud action. Preserve source before initialization. `azd provision`, `azd deploy` and `azd down` create billable resources, publish code and delete resources respectively; none is a build command or authorized by these instructions.

## 04: a decision agent without a conversational model

The exact MIT-licensed [public sample](https://github.com/microsoft/Generative-AI-for-beginners-dotnet/tree/00b60a8a95ad57d5ef975bb914a87f1cec76e049/samples/CoreSamples/DecisionAgent-01-FoundryHosted) is retained with its source, pins, LICENSE and THIRD-PARTY-NOTICES. Follow its README for its separate configuration:

- `Decision__Endpoint`: native decision resource root, not `/api/projects/...`
- `Decision__Deployment`: existing Microsoft-Decision-1 deployment, not the chat model

The existing two-prompt helper is intentionally unchanged and does not configure the decision model. The sample manifest maps `DECISION_ENDPOINT` and `DECISION_DEPLOYMENT` to those runtime keys.

This sample uses AzureCliCredential locally and ManagedIdentityCredential with the agent's system-assigned identity in Foundry, exactly as upstream. That narrow source-fidelity exception differs from stage 03's DefaultAzureCredential policy. The upstream `CreateBuilder(args)`/environment-based hosted identity selection is preserved, not introduced as a general demo pattern.

An agent can implement rules or traditional ML without an LLM. This example specifically uses structured Microsoft-Decision-1 scoring without a chat/generative explanation call. Microsoft describes Decision-1 as built on Qwen3.5-9B, so do not claim this example has no language-model-derived component anywhere. Its answer text is deterministic and a person must confirm the route. Confidence and class probability are distinct, neither guarantees correctness.

Offline checks from stage 04:

```powershell
dotnet test .\tests\SupportRoutingAgent.Tests -c Release --no-restore
```

All 26 original tests use fake decisions/HTTP/credentials and loopback Responses hosting. They prove neither real inference nor cloud identity permissions. No model, RBAC or deployment operation was executed. Do not follow upstream installation/login/deployment/cleanup commands without separate approval.

Official references: [hosting integration](https://learn.microsoft.com/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp), [hosted-agent concepts](https://learn.microsoft.com/azure/foundry/agents/concepts/hosted-agents), [Decision-1 model background](https://labs.ai.azure.com/innovations/microsoft-decision-1/).
